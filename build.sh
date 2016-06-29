#!/usr/bin/env bash

while true ; do
	case "$1" in
		-c|--clear-cache) CLEAR_CACHE=1 ; shift ;;
		--) shift ; break ;;
		*) shift ; break ;;
	esac
done

RESULTCODE=0

# Download the CLI install script
echo "Installing dotnet CLI"
mkdir -p .dotnetcli
curl -o .dotnetcli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/dotnet-install.sh

# Run dotnet-install.sh
chmod +x .dotnetcli/dotnet-install.sh
.dotnetcli/dotnet-install.sh -i .dotnetcli -c preview -v 1.0.0-preview2-003121

# Display current version
DOTNET="$(pwd)/.dotnetcli/dotnet"
$DOTNET --version

echo "================="

# clear caches
if [ "$CLEAR_CACHE" == "1" ]
then
	# echo "Clearing the nuget web cache folder"
	# rm -r -f ~/.local/share/NuGet/*

	echo "Clearing the nuget packages folder"
	rm -r -f ~/.nuget/packages/*
fi

# restore packages
echo "$DOTNET restore src test --verbosity minimal"
$DOTNET restore src test --verbosity minimal
if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

# run tests
for testProject in `find test -type f -name project.json`
do
	testDir="$(pwd)/$(dirname $testProject)"

	pushd $testDir

	echo "$DOTNET test $testDir --configuration release --framework netcoreapp1.0"
	$DOTNET test $testDir --configuration release

	if [ $? -ne 0 ]; then
		echo "$testDir FAILED"
		RESULTCODE=1
	fi

	popd

done

exit $RESULTCODE
