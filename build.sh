#!/usr/bin/env bash

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

TOOLS_DIR="$SCRIPT_DIR/tools"
if [ ! -d "$TOOLS_DIR" ]; then
  mkdir "$TOOLS_DIR"
fi

# this one works inside command substititution, because it writes to stderr
error_exit() {
  echo "$1" 1>&2
  exit 1
}

###########################################################################
# LOAD versions from build.config
###########################################################################

trim() {
  local var="$*"
  # remove leading whitespace characters
  var="${var#"${var%%[![:space:]]*}"}"
  # remove trailing whitespace characters
  var="${var%"${var##*[![:space:]]}"}"
  printf '%s' "$var"
}

source "$SCRIPT_DIR"/build.config
# For some reason we have to remove whitespace
DOTNET_VERSION=$(trim "$DOTNET_VERSION")
CAKE_VERSION=$(trim "$CAKE_VERSION")
CAKESCRIPTS_VERSION=$(trim "$CAKESCRIPTS_VERSION")
GITVERSION_VERSION=$(trim "$GITVERSION_VERSION")
GITRELEASEMANAGER_VERSION=$(trim "$GITRELEASEMANAGER_VERSION")

if [[ ! "$DOTNET_VERSION" ]]; then
  error_exit "Failed to parse .NET Core SDK version"
fi
if [[ ! "$CAKE_VERSION" ]]; then
  error_exit "Failed to parse Cake version"
fi
if [[ ! "$CAKESCRIPTS_VERSION" ]]; then
  error_exit "Failed to parse CakeScripts version"
fi
if [[ ! "$GITVERSION_VERSION" ]]; then
  error_exit "Failed to parse GitVersion version"
fi
if [[ ! "$GITRELEASEMANAGER_VERSION" ]]; then
  error_exit "Failed to parse GitReleaseManager version"
fi

###########################################################################
# INSTALL .NET Core SDK
###########################################################################

function parse_sdk_version() {
  local major
  local minor
  local featureAndPatch
  local feature
  local patch
  local version="$*"
  major=$(echo "$version" | cut -d. -f1)
  minor=$(echo "$version" | cut -d. -f2)
  featureAndPatch=$(echo "$version" | cut -d. -f3)
  feature=$(echo "$featureAndPatch" | cut -c1-1)
  patch=$(echo "$featureAndPatch" | cut -c2-3)
  echo "$major" "$minor" "$feature" "$patch"
}

DOTNET_CHANNEL='LTS'

export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0
export DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX=2

if ! DOTNET_INSTALLED_VERSION=$(dotnet --version); then
  # Extract the first line of the message without making bash write any error messages
  echo "$DOTNET_INSTALLED_VERSION" | head -1
  echo "That is not problem, we will install the SDK version below."
  DOTNET_INSTALLED_VERSION='0.0.000' # Force installation of .NET Core SDK via dotnet-install script
else
  echo ".NET Core SDK version ${DOTNET_INSTALLED_VERSION} found."
fi

echo ".NET Core SDK version ${DOTNET_VERSION} is required (with roll forward to latest patch policy)"

# Parse the sdk versions into major, minor, feature and patch (x.y.znn)
read -r major minor feature patch < <(parse_sdk_version "$DOTNET_VERSION")
read -r foundMajor foundMinor foundFeature foundPatch < <(parse_sdk_version "$DOTNET_INSTALLED_VERSION")

# latestPatch roll forward policy
if [[ "$foundMajor" != "$major" ]] || \
   [[ "$foundMinor" != "$minor" ]] || \
   [[ "$foundFeature" != "$feature" ]] || \
   [[ "$foundPatch" -lt "$patch" ]]; then

  echo "Installing .NET Core SDK version ${DOTNET_VERSION} ..."
  if [ ! -d "$SCRIPT_DIR/.dotnet" ]; then
    mkdir "$SCRIPT_DIR/.dotnet"
  fi
  curl -Lsfo "$SCRIPT_DIR/.dotnet/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh > /dev/null 2>&1
  bash "$SCRIPT_DIR/.dotnet/dotnet-install.sh" --version "$DOTNET_VERSION" --channel $DOTNET_CHANNEL --install-dir .dotnet --no-path >/dev/null 2>&1
  # Note: This PATH/DOTNET_ROOT will be visible only when sourcing script.
  # Note: But on travis CI or other *nix build machines the PATH does not have
  #       to be visible on the commandline after the build
  export PATH="$SCRIPT_DIR/.dotnet:$PATH"
  export DOTNET_ROOT="$SCRIPT_DIR/.dotnet"
fi

###########################################################################
# INSTALL .NET Core 3.x tools
###########################################################################

function install_tool() {
  local packageId=$1
  local toolCommand=$2
  local version=$3

  local toolPath="$TOOLS_DIR/.store/${packageId}/${version}"
  local exePath="$TOOLS_DIR/${toolCommand}"

  if [ ! -d "$toolPath" ] || [ ! -f "$exePath" ]; then

    if [ -f "$exePath" ]; then

      if ! dotnet tool uninstall --tool-path "$TOOLS_DIR" "$packageId" >/dev/null 2>&1; then
        error_exit "Failed to uninstall ${packageId}"
      fi
    fi

    if ! dotnet tool install --tool-path "$TOOLS_DIR" --version "$version" --configfile NuGet.public.config "$packageId" >/dev/null 2>&1; then
      error_exit "Failed to install ${packageId}"
    fi

  fi

  # return value to be read via command substitution
  echo "$exePath"
}

# We use lower cased package ids, because toLower is not defined in bash
CAKE_EXE=$(install_tool 'cake.tool' 'dotnet-cake' "$CAKE_VERSION")
install_tool 'gitversion.tool' 'dotnet-gitversion' "$GITVERSION_VERSION" >/dev/null 2>&1
install_tool 'gitreleasemanager.tool' 'dotnet-gitreleasemanager' "$GITRELEASEMANAGER_VERSION" >/dev/null 2>&1

###########################################################################
# INSTALL CakeScripts
###########################################################################

if [ ! -d "$TOOLS_DIR/Maxfire.CakeScripts" ]; then
  # latest or empty string
  if [[ $CAKESCRIPTS_VERSION == "latest" ]] || [[ -z "$CAKESCRIPTS_VERSION" ]]; then
    mono tools/nuget.exe install Maxfire.CakeScripts -ExcludeVersion -Prerelease -OutputDirectory "$TOOLS_DIR" -Source 'https://api.nuget.org/v3/index.json;https://www.myget.org/F/maxfire/api/v3/index.json' >/dev/null 2>&1
  else
    mono tools/nuget.exe install Maxfire.CakeScripts -Version "$CAKESCRIPTS_VERSION" -ExcludeVersion -Prerelease -OutputDirectory "$TOOLS_DIR" -Source 'https://api.nuget.org/v3/index.json;https://www.myget.org/F/maxfire/api/v3/index.json' >/dev/null 2>&1
  fi

  # shellcheck disable=SC2181
  if [ $? -ne 0 ]; then
    error_exit "Failed to install Maxfire.CakeScripts"
  fi
fi

###########################################################################
# RUN BUILD SCRIPT
###########################################################################

echo "Running build script..."
(exec "$CAKE_EXE" build.cake --bootstrap) && (exec "$CAKE_EXE" build.cake "$@")
