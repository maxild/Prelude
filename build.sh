#!/usr/bin/env bash

while true ; do
	case "$1" in
		-c|--clear-cache) CLEAR_CACHE=1 ; shift ;;
		--) shift ; break ;;
		*) shift ; break ;;
	esac
done

RESULTCODE=0

configuration="release"

rootFolder=$(dirname "${BASH_SOURCE}")
artifactsFolder="./artifacts"

# clear artifacts
if [[ -d $artifactsFolder ]]; then  
  rm -R $artifactsFolder
fi

# One liner...
# curl -sSL https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0-preview1/scripts/obtain/dotnet-install.sh | bash /dev/stdin --version 1.0.0-preview1-002702 --install-dir ~/dotnet

# Download the CLI install script
echo "Installing dotnet CLI"
mkdir -p .dotnetcli
curl -o .dotnetcli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0-preview2/scripts/obtain/dotnet-install.sh

# Define the install root for the script (it defaults to ~/.dotnet)
export DOTNET_INSTALL_DIR="$PWD/.dotnetcli"

# Define the DOTNET_HOME directory for tools in the CLI
export DOTNET_HOME=$DOTNET_INSTALL_DIR

# Run dotnet-install.sh
chmod +x .dotnetcli/dotnet-install.sh
.dotnetcli/dotnet-install.sh --install-dir .dotnetcli -channel preview --version 1.0.0-preview2-003121 --no-path

# Display current version
DOTNET="$DOTNET_INSTALL_DIR/dotnet"
$DOTNET --version

# clear caches
if [ "$CLEAR_CACHE" == "1" ]
then
	# echo "Clearing the nuget web cache folder"
	# rm -r -f ~/.local/share/NuGet/*

	echo "Clearing the nuget packages folder"
	rm -r -f ~/.nuget/packages/*
fi

# restore packages
echo "dotnet restore src test --verbosity minimal"
$DOTNET restore src test --verbosity minimal

if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

# run tests
for testProject in `find test -type f -name project.json`
do
    # Get the test project name 
	testDir="$(pwd)/$(dirname $testProject)"
    testProjectName="$(basename $testDir)"

    echo "Running tests for $testProjectName"

	pushd $testDir

    # Ideally we would use the 'dotnet test' command to test both netcoreapp1.0 and net46,
    # but this currently doesn't work due to https://github.com/dotnet/cli/issues/3073
	echo "dotnet test $testDir --configuration $configuration --framework netcoreapp1.0"
	$DOTNET test $testDir --configuration $configuration --framework netcoreapp1.0

    if [ $? -ne 0 ]; then
        echo "$testProjectName failed on .NET Core!!"
        RESULTCODE=1
    fi

    # Instead, build with .NET Core SDK (dotnet build)... 
    echo "dotnet build $testDir --configuration $configuration --framework net46"
	$DOTNET build $testDir --configuration $configuration --framework net46

    if [ $? -ne 0 ]; then
        echo "$testProjectName failed to build for net46!!"
        RESULTCODE=1
    fi

    # ..., and run xUnit.net .NET CLI test runner directly with mono for the full/desktop .net version
    echo "mono ./bin/$configuration/net46/*/dotnet-test-xunit.exe ./bin/$configuration/net46/*/$testProjectName.dll"
    mono $testDir/bin/$configuration/net46/*/dotnet-test-xunit.exe $testDir/bin/$configuration/net46/*/$testProjectName.dll

    if [ $? -ne 0 ]; then
        echo "$testProjectName failed on Mono!!"
        RESULTCODE=1
    fi

	popd

done

if [ $RESULTCODE -ne 0 ]; then
	echo "Tests failed!!"
	exit $RESULTCODE 
fi

#TODO: revision from args (local-xxxx default)
#revision=${TRAVIS_JOB_ID:=1}  
#revision=$(printf "%04d" $revision)
revision="local-12345"

for srcProject in `find src -type f -name project.json`
do
    # Get the project name 
	srcDir="$(pwd)/$(dirname $srcProject)"
    srcProjectName="$(basename $srcDir)"

    echo "Build package for $srcProjectName"
    echo "================="
    
    echo "dotnet pack $srcDir -c $configuration -o $artifactsFolder --version-suffix=$revision"
    $DOTNET pack $srcDir -c $configuration -o $artifactsFolder --version-suffix=$revision  

    if [ $? -ne 0 ]; then
        echo "$srcProjectName failed to pack!!"
        RESULTCODE=1
    fi

done

exit $RESULTCODE
