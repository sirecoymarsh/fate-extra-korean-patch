"""Make a copy of a Fate/EXTRA Korean ISO with a larger file-loader heap (heap 3) and a matching libc heap.
Why: the Korean PRELOAD keeps ~229 KB more resident in heap 3 than the Japanese one (ruby font + subtitle hook),
so heavy scenes (round-3-end projector demo battle, Saber/Archer) cannot fit the servant interface model and the
loader retries forever ("Now Loading"). Heap 3 is carved from libc malloc at boot, so both sizes grow by the same amount
and every other malloc user keeps exactly the free space it had in the Japanese game.

  python build_heapfix.py <in.iso> <out.iso> [--extra-kb 512]

Sites (both EBOOT copies in the ISO: PSP_GAME/SYSDIR/EBOOT.BIN and the stale BOOT.BIN):
  EBOOT 0x0297C  lui a1,0x0124  (heap 3 size 0x01240000, runtime 0x088068DC)
  EBOOT 0x173E20 u32 0x4E00     (sce_newlib_heap_kb_size = 19,968 KB, runtime 0x08977D80)"""
import sys, shutil, struct, hashlib, json, argparse
from pathlib import Path

HEAP3_LUI = 0x3C050124          # lui a1, 0x0124
NEWLIB_KB = 0x4E00


def find_all(data, needle):
    out, i = [], data.find(needle)
    while i != -1:
        out.append(i)
        i = data.find(needle, i + 1)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('src'); ap.add_argument('dst')
    ap.add_argument('--extra-kb', type=int, default=512)
    a = ap.parse_args()
    assert a.extra_kb % 64 == 0, 'heap 3 size is set with lui (64 KB steps)'
    src, dst = Path(a.src), Path(a.dst)
    data = src.read_bytes()
    # anchor each site by its surrounding bytes so both EBOOT copies are found and nothing else
    lui_old = struct.pack('<I', HEAP3_LUI)
    lui_new = struct.pack('<I', HEAP3_LUI + a.extra_kb // 64)
    kb_old = struct.pack('<I', NEWLIB_KB)
    kb_new = struct.pack('<I', NEWLIB_KB + a.extra_kb)
    # heap-3 call site: ori a0,zero,0x400 ; lui a1,0x124 ; lui a2,8 ; lui a3,0xa
    site = bytes.fromhex('00040434') + lui_old + bytes.fromhex('0800063c0a00073c')
    heap_hits = [h + 4 for h in find_all(data, site)]
    # newlib heap size word: followed by 0x000000C8 in the same data block (checked live at 0x08977D84)
    kb_hits = [h for h in find_all(data, kb_old + struct.pack('<I', 0xC8))]
    assert len(heap_hits) == 2, heap_hits
    assert len(kb_hits) == 2, kb_hits
    shutil.copyfile(src, dst)
    with open(dst, 'r+b') as f:
        for h in heap_hits:
            f.seek(h); f.write(lui_new)
        for h in kb_hits:
            f.seek(h); f.write(kb_new)
    out = dst.read_bytes()
    assert len(out) == len(data)
    diff = []
    CH = 1 << 20
    for c in range(0, len(data), CH):
        if data[c:c + CH] != out[c:c + CH]:
            diff += [c + i for i in range(min(CH, len(data) - c)) if data[c + i] != out[c + i]]
    assert all(any(h <= i < h + 4 for h in heap_hits + kb_hits) for i in diff), 'unexpected change'
    man = dict(src=str(src), src_sha256=hashlib.sha256(data).hexdigest().upper(), dst=str(dst),
               dst_sha256=hashlib.sha256(out).hexdigest().upper(), extra_kb=a.extra_kb,
               heap3_size=hex(0x01240000 + a.extra_kb * 1024), newlib_kb=hex(NEWLIB_KB + a.extra_kb),
               heap_sites=[hex(h) for h in heap_hits], newlib_sites=[hex(h) for h in kb_hits], changed_bytes=len(diff))
    Path(str(dst) + '.manifest.json').write_text(json.dumps(man, indent=1), 'utf-8')
    print(json.dumps(man, indent=1))


if __name__ == '__main__':
    main()
