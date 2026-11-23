# Europe version fix (black screen)

**Symptom:** the Europe version (SLES_549.51) showed a black screen with sound and the frame counter still running, and never uploaded a texture. The USA version (SLUS_216.61) worked.

**Cause:** a race in the SIF emulation of the runtime. The game loader starts an asynchronous streaming request and sets an in-flight flag after the call returns. The completion callback clears that flag. On the Europe version the last request was already in the IOP buffer, so the answer arrived inside the same call: the callback cleared the flag first, then the game set it again. Nothing cleared it a second time, so the loader waited forever. The USA version always has a real disc read in between, which hides the race.

**Fix:** in runtime/src/iop/sif.cpp, in pump(), IOP-to-EE user commands (not RPC-end packets) are held for a short time before delivery, so the caller flag write always happens first. The default hold is 3000 runtime clock units. The environment variable PS2_SIF_HOLD overrides it, and 0 turns the hold off.

**Results (3600 frames):** Europe 3495 texture uploads and CLUT loads, USA 3493 of each. The USA version is unaffected.

**Rebuild note:** the runtime is a separate build. After editing runtime sources, rebuild the runtime first, then delete the game ps2run.exe and rebuild the game.
