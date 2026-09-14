# Third-party native libraries

This folder bundles the [BASS](https://www.un4seen.com/bass.html) audio library and its BASSMIDI
and BASSVST add-ons (`bass.dll`, `bassmidi.dll`, `bass_vst.dll`, x86 and x64), used for
SoundFont-based playback (`JianpuEditor/Services/BassMidiSynthesizer.cs`) and the optional VST2
instrument-plugin playback engine (`JianpuEditor/Services/BassVstSynthesizer.cs`).

**Licensing — read before distributing a build of this project:**

BASS/BASSMIDI is free to use only for a non-commercial entity (e.g. an individual) not making
money from the product (through sales, advertising, etc). If you or your fork of this project
sells the software, monetizes it (ads, subscriptions, etc.), or is a commercial entity, you must
purchase a BASS license from un4seen.com — this is separate from and unaffected by this
project's own Apache-2.0 license, which does not cover this bundled third-party dependency.

See the current terms at <https://www.un4seen.com/bass.html> (Licence section) before relying
on this for anything beyond personal/hobby use.
