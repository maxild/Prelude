# Prelude

The Prelude repository contains extensions to the .NET Framework (`netstandard2.0`) and .NET 6 (`net6.0`) used by several projects written by Morten Maxild.

## Build Status


| Build server                | Platform         | Branch       | Build status                                                                                                                                                        |
|-----------------------------|------------------|--------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| AppVeyor                    | Windows          | Any branch   | [![Current Status](https://ci.appveyor.com/api/projects/status/kkcqonha77p6dj2l?svg=true)](https://ci.appveyor.com/project/maxild/prelude)
| AppVeyor                    | Windows          | `dev` branch | [![Dev branch status](https://ci.appveyor.com/api/projects/status/kkcqonha77p6dj2l/branch/dev?svg=true)](https://ci.appveyor.com/project/maxild/prelude/branch/dev) |
| GitHub Actions              | Windows / Linux  | `dev` branch | [![Dev branch status](https://github.com/maxild/Prelude/actions/workflows/main.yml/badge.svg?branch=dev)](https://github.com/maxild/Prelude/actions)                    |



## Get Packages

Stable releases can be found on NuGet.org. Our CI build also produces packages, that can be found on our MyGet feed.

### Stable releases

[![NuGet](https://img.shields.io/nuget/v/Maxfire.Prelude.Core.svg?label=Maxfire.Prelude.Core)](https://www.nuget.org/packages/Maxfire.Prelude.Core/)
[![NuGet](https://img.shields.io/nuget/v/Maxfire.Prelude.ComponentModel.TypeConverter.svg?label=Maxfire.Prelude.ComponentModel.TypeConverter)](https://www.nuget.org/packages/Maxfire.Prelude.ComponentModel.TypeConverter/)

Stable releases are published to [NuGet.org](https://www.nuget.org/packages?q=Maxfire.Prelude).

### Unstable releases (pre-releases)

[![MyGet](https://img.shields.io/myget/maxfire-ci/vpre/Maxfire.Prelude.Core.svg?label=Maxfire.Prelude.Core)](https://www.myget.org/feed/maxfire-ci/package/nuget/Maxfire.Prelude.Core)
[![MyGet](https://img.shields.io/myget/maxfire-ci/vpre/Maxfire.Prelude.ComponentModel.TypeConverter.svg?label=Maxfire.Prelude.ComponentModel.TypeConverter)](https://www.myget.org/feed/maxfire-ci/package/nuget/Maxfire.Prelude.ComponentModel.TypeConverter)

If you're feeling adventurous, [continuous integration builds are on MyGet](https://www.myget.org/gallery/maxfire-ci).
