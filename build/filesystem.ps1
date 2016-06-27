Function Get-FileName {
    param([string]$path)
    Split-Path -Path $path -Leaf
}

Function Get-DirName {
    param([string]$path)
    Split-Path -Path $path -Parent
}

Function Test-Dir {
    param([string]$path)
    Test-Path -PathType Container -Path $path
}

Function Test-File {
    param([string]$path)
    Test-Path -PathType Leaf -Path $path
}

Function Create-Dir {
    param([string]$path)
    New-Item -ItemType directory -Path $path
}
