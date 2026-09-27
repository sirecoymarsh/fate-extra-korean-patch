# v8h source reproduction

The source is the immutable Japanese ISO. The component is the hash-bound v8g VCDIFF from the prior public base package. The exact 15-word plan produces the same v8h image validated here; no patched ISO is a build input.

```powershell
python rebuild.py --source ORIGINAL.iso --component Fate-Extra-Korean-v8g.xdelta --plan v8h-opcode-plan.json --xdelta xdelta3.exe --output Fate-Extra-Korean-v8h.iso
```

The plan binds source, component and target hashes, nonoverlapping ranges and exact before/after bytes. The generated ISO must remain private. The release ZIP contains the normal original-to-v8h patch for end users.
