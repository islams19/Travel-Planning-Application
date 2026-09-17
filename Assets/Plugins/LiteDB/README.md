# LiteDB dependency

- Version: 5.0.21 (MIT license; see LICENSE.txt).
- Source: https://api.nuget.org/v3-flatcontainer/litedb/5.0.21/litedb.5.0.21.nupkg
- Assembly: `lib/netstandard2.0/LiteDB.dll` from that package, unmodified.
- DLL SHA-256: `AE31AC6A93549217B9E8B81497D1EE831658ADE6DF9E1EE20F2838F22DE5E218`.
- Purpose: local account persistence and a unique email index.
- System.Buffers is supplied by Unity's managed runtime. The standalone test runner copies Unity's facade into its temporary output directory.

Included directly so teammates can open the Unity project without a separate NuGet installation step. Do not add another version of LiteDB to the same project. Unity desktop import/player validation is still required; this milestone does not claim WebGL or IL2CPP compatibility.
