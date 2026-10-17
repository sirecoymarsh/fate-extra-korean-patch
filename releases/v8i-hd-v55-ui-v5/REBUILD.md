# v8i source reproduction

The source is the immutable Japanese ISO. The component is the hash-bound v8g VCDIFF from the v8g public base package.
The plan applies the 15 v8h opcode restorations and the 4 v8i heap-size bytes in one stream; no patched ISO is a build input.

```powershell
python rebuild.py --source ORIGINAL.iso --component Fate-Extra-Korean-v8g.xdelta --plan v8i-heap-plan.json --xdelta xdelta3.exe --output Fate-Extra-Korean-v8i.iso
```

The plan binds source, component and target hashes, nonoverlapping ranges and exact before/after bytes. The generated ISO must remain private. The release ZIP contains the normal original-to-v8i patch for end users.
