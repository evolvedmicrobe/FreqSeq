#!/bin/sh
rm -rf build; mkdir build


### Build Bio dependency
## Note this requires having mono with the PCL assemblies installed into:
# MONO_PATH/lib/mono/xbuild-frameworks/
# if they aren't there, you will likely have to copy over from a machine that has them
# on Mac OSX they are located at the location below
# /Library/Frameworks/Mono.framework/Versions/4.0.0/lib/mono/xbuild-frameworks
xbuild /p:Configuration=Release FreqSeq/FREQSeq.csproj
cp FreqSeq/bin/Release/* build/
xbuild /p:Configuration=Release freqout/freqout.csproj
cp freqout/bin/Release/* build/

# Now make a bundled executable
cd build
export PKG_CONFIG_PATH=$HOME/mono64/lib/pkgconfig/
mkbundle --keeptemp --static  --deps --config-dir /nothing --config /home/UNIXHOME/ndelaney/mono64/etc/mono/config -o freqout freqout.exe FreqSeq.dll