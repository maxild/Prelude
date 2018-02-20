#!/usr/bin/env bash

##########################################################################
# This is the Cake bootstrapper script for Linux and OS X.
# This file was downloaded from https://github.com/cake-build/resources
# Feel free to change this file to fit your needs.
##########################################################################

# Define directories.
SCRIPT_DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )

DOTNET_DIR="$SCRIPT_DIR/.dotnet"
TOOLS_DIR="$SCRIPT_DIR/tools"
NUGET_EXE="$TOOLS_DIR/nuget.exe"
CAKE_EXE="$TOOLS_DIR/Cake/Cake.exe"
PACKAGES_CONFIG="$TOOLS_DIR/packages.config"
PACKAGES_CONFIG_MD5="$TOOLS_DIR/packages.config.md5sum"

DOTNET_CHANNEL="preview"
DOTNET_VERSION="1.0.0-preview2-003121"
DOTNET_CHANNEL_INSTALLER_URL="https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0-preview2/scripts/obtain/dotnet-install.sh"

# Define md5sum or md5 depending on Linux/OSX
MD5_EXE=
if [[ "$(uname -s)" == "Darwin" ]]; then
    MD5_EXE="md5 -r"
else
    MD5_EXE="md5sum"
fi

# Define default arguments.
SCRIPT="build.cake"
TARGET="Default"
CONFIGURATION="Release"
VERBOSITY="verbose"
DRYRUN=
NUGET_VERSION="latest"
CAKESCRIPTS_VERSION="latest"
SHOW_VERSION=false
SCRIPT_ARGUMENTS=()

# Parse arguments.
for i in "$@"; do
    case $1 in
        -s|--script) SCRIPT="$2"; shift ;;
        -t|--target) TARGET="$2"; shift ;;
        -c|--configuration) CONFIGURATION="$2"; shift ;;
        -v|--verbosity) VERBOSITY="$2"; shift ;;
        --nugetVersion) NUGET_VERSION="$2"; shift ;;
        --cakeScriptsVersion) CAKESCRIPTS_VERSION="$2"; shift ;;
        -d|--dryrun) DRYRUN="--dryrun" ;;
        --version) SHOW_VERSION=true ;;
        --) shift; SCRIPT_ARGUMENTS+=("$@"); break ;;
        *) SCRIPT_ARGUMENTS+=("$1") ;;
    esac
    shift
done

if [[ $NUGET_VERSION != "latest" ]] && [[ $NUGET_VERSION =~ ^v.* ]]; then
    $NUGET_VERSION="v$NUGET_VERSION"
fi

NUGET_URL="https://dist.nuget.org/win-x86-commandline/$NUGET_VERSION/nuget.exe"

# Make sure the tools folder exist.
if [ ! -d "$TOOLS_DIR" ]; then
  mkdir "$TOOLS_DIR"
fi

###########################################################################
# Install .NET Core CLI
###########################################################################

echo "Installing .NET Core SDK Binaries..."
if [ ! -d "$SCRIPT_DIR/.dotnet" ]; then
  mkdir "$SCRIPT_DIR/.dotnet"
fi
curl -Lsfo "$DOTNET_DIR/dotnet-install.sh" "$DOTNET_CHANNEL_INSTALLER_URL"
sudo chmod +x "$DOTNET_DIR/dotnet-install.sh"
sudo bash "$DOTNET_DIR/dotnet-install.sh" --channel "$DOTNET_CHANNEL" --version "$DOTNET_VERSION" --install-dir "$DOTNET_DIR" --no-path
export PATH="$DOTNET_DIR":$PATH
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
"$DOTNET_DIR/dotnet" --info

###########################################################################
# INSTALL NUGET
###########################################################################

# Download NuGet if it does not exist.
if [ ! -f "$NUGET_EXE" ]; then
    echo "Downloading NuGet ($NUGET_VERSION)..."

    curl -Lsfo "$NUGET_EXE" "$NUGET_URL"
    if [ $? -ne 0 ]; then
        echo "An error occured while downloading nuget.exe."
        exit 1
    fi

    echo ($NUGET_EXE help | head -n 1)
fi

###########################################################################
# INSTALL CAKE
###########################################################################

# Install/restore tools (i.e. Cake) using NuGet
pushd "$TOOLS_DIR" >/dev/null

# Check for changes in packages.config and remove installed tools if true.
if [ ! -f $PACKAGES_CONFIG_MD5 ] || [ "$( cat $PACKAGES_CONFIG_MD5 | sed 's/\r$//' )" != "$( $MD5_EXE $PACKAGES_CONFIG | awk '{ print $1 }' )" ]; then
    find . -type d ! -name . | xargs rm -rf
fi

mono "$NUGET_EXE" install $PACKAGES_CONFIG -ExcludeVersion
if [ $? -ne 0 ]; then
    echo "Could not restore NuGet packages."
    exit 1
fi

# save packages.config hash to disk
$MD5_EXE $PACKAGES_CONFIG | awk '{ print $1 }' >| $PACKAGES_CONFIG_MD5

# Install re-usable cake scripts
# Note: We cannot put the package reference into ./tools/packages.json, because this file does not support floating versions
if [ ! -d "$TOOLS_DIR/Maxfire.CakeScripts" ]; then
    # latest or empty string
    if [[ $CAKESCRIPTS_VERSION == "latest" ]] || [[ -z "$CAKESCRIPTS_VERSION" ]]; then
        mono "$NUGET_EXE" install Maxfire.CakeScripts -ExcludeVersion -Prerelease -Source 'https://api.nuget.org/v3/index.json;https://www.myget.org/F/maxfire/api/v3/index.json'
    else
        mono "$NUGET_EXE" install Maxfire.CakeScripts -Version "$CAKESCRIPTS_VERSION" -ExcludeVersion -Prerelease -Source 'https://api.nuget.org/v3/index.json;https://www.myget.org/F/maxfire/api/v3/index.json'
    fi
fi

popd >/dev/null

# Make sure that Cake has been installed.
if [ ! -f "$CAKE_EXE" ]; then
    echo "Could not find Cake.exe at '$CAKE_EXE'."
    exit 1
fi

###########################################################################
# RUN BUILD SCRIPT
###########################################################################

# Start Cake
if $SHOW_VERSION; then
    exec mono "$CAKE_EXE" --version
else
# C# v6 features (e.g. string interpolation) are not supported without '-experimental' flag
#   See https://github.com/cake-build/cake/issues/293
#   See https://github.com/cake-build/cake/issues/326
# TODO: Is -experimental necessary on mono?
    #exec mono "$CAKE_EXE" $SCRIPT --experimental --verbosity=$VERBOSITY --configuration=$CONFIGURATION --target=$TARGET $DRYRUN "${SCRIPT_ARGUMENTS[@]}"
    echo "exec mono $CAKE_EXE $SCRIPT --verbosity=$VERBOSITY --configuration=$CONFIGURATION --target=$TARGET $DRYRUN ${SCRIPT_ARGUMENTS[@]}"
    exec mono "$CAKE_EXE" $SCRIPT --verbosity=$VERBOSITY --configuration=$CONFIGURATION --target=$TARGET $DRYRUN "${SCRIPT_ARGUMENTS[@]}"
fi
