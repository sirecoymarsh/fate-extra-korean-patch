"""Rebuild v8g from an original Japanese ISO, adopted v8f VCDIFF and fixed plan.

The v8f patched ISO is never a build input. The component is decoded from the
immutable original and transformed in the same stream with exact before checks.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess

def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(8 << 20), b''):
            h.update(block)
    return h.hexdigest().upper()

def main():
    ap = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'component', 'plan', 'xdelta', 'output'):
        ap.add_argument('--' + name, type=Path, required=True)
    a = ap.parse_args()
    p = json.loads(a.plan.read_text('utf-8'))
    if a.output.exists() or a.output.with_suffix('.building').exists():
        raise ValueError('Refusing to replace an existing output or partial build')
    assert a.source.stat().st_size == p['source']['bytes']
    assert digest(a.source) == p['source']['sha256']
    assert digest(a.component) == p['component']['sha256']
    spans = []
    end = 0
    for x in p['patches']:
        at, before, after = x['offset'], bytes.fromhex(x['before']), bytes.fromhex(x['after'])
        assert at >= end and len(before) == len(after) > 0
        end = at + len(before)
        assert end <= p['component']['decoded_bytes']
        spans.append((at, before, after))
    partial = a.output.with_suffix('.building')
    base_hash, output_hash = hashlib.sha256(), hashlib.sha256()
    pos, seen = 0, set()
    with partial.open('xb') as f:
        proc = subprocess.Popen([str(a.xdelta), '-d', '-c', '-s', str(a.source), str(a.component)],
                                stdout=subprocess.PIPE, creationflags=0x08000000 if os.name == 'nt' else 0)
        try:
            while block := proc.stdout.read(8 << 20):
                base_hash.update(block)
                buf = bytearray(block)
                for i, (at, before, after) in enumerate(spans):
                    lo, hi = max(pos, at), min(pos + len(block), at + len(after))
                    if hi <= lo:
                        continue
                    assert block[lo-pos:hi-pos] == before[lo-at:hi-at], ('Before mismatch', i)
                    buf[lo-pos:hi-pos] = after[lo-at:hi-at]
                    seen.add(i)
                f.write(buf)
                output_hash.update(buf)
                pos += len(block)
            proc.stdout.close()
            assert proc.wait() == 0
        except BaseException:
            proc.kill()
            proc.wait()
            raise
        f.flush()
        os.fsync(f.fileno())
    assert pos == p['component']['decoded_bytes'] == p['target']['bytes']
    assert base_hash.hexdigest().upper() == p['component']['decoded_sha256']
    assert len(seen) == len(spans)
    assert output_hash.hexdigest().upper() == p['target']['sha256'] == digest(partial)
    partial.replace(a.output)
    print('PASS', p['target']['sha256'])

if __name__ == '__main__':
    main()
