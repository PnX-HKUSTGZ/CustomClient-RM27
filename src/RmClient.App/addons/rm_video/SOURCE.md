# rm_video Native GDExtension Provenance

- Upstream repository: https://github.com/PnX-HKUSTGZ/rm_udp_streamer
- Upstream commit: `1bbcfaa9e503d32b7279c2d7a409e987eda12236` (2026-11-17); build metadata in `prebuilt.json`
- Included files: `addons/rm_video/` verbatim (`bin/rm_video.dll`, `bin/librm_video.so`, `rm_video.gdextension`, LGPL compliance artifacts in `redistribution/`)
- Verified SHA256 (copied 2026-11-23): `bin/rm_video.dll` (`1a8788fe...b93e7`), `bin/librm_video.so` (`5e75ed63...797e`) matching `prebuilt.json`
- Registered native classes: `RmVideoSession` (UDP receive, reassembly, HEVC decode on native threads), `RmVideoSurface` (aspect-preserving presentation Control, CPU upload or CUDA/OpenGL bridge)
- Wrapped exclusively by `scripts/Video/VideoLink.cs`; do not modify native binaries directly.
- FFmpeg is statically linked under LGPL-2.1-or-later; keep `redistribution/` alongside distributed binaries.
