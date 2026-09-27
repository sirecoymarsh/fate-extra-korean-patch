# v8g cumulative source build

Build from the immutable Japanese ISO and the adopted v8f VCDIFF component (from the previous public base ZIP). No patched ISO is an input. The fixed plan identifies every byte span, exact before/after bytes, input hashes and expected output hash. Any source, overlap, before-byte or final hash mismatch stops the build.

```powershell
python rebuild.py --source ORIGINAL.iso --component Fate-Extra-Korean-v8f.xdelta --plan adopted-native-plan.json --xdelta xdelta3.exe --output Fate-Extra-Korean-v8g.iso
```

Requires Python 3.10+ and the xdelta3 tool bundled in the launcher. The output ISO is private and must not be committed or redistributed. The already encoded v8g delta in the current base pack is the normal installation path.

The fixed spans retain the approved v2/v45/v47/v49a native work. Shop X shifts are replaced by centered opt-in art; status X is retained with right-aligned opt-in art and the approved Y adjustment. This preserves the native English UI when Korean UI is off. The shared status/result label stays within its original glyph rectangle. Both CANCEL consumers use the same native height.

The native MIPS checks and pointer/decoder checks used the final v8g bytes. They do not claim full game-scene replay. Adopted translation provenance is recorded in the plan; no new semantic translation was generated for this packaging change.
