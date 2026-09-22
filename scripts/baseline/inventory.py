#!/usr/bin/env python3
"""Capture source/project dependencies and shipped assets without executing Flow."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=root).decode().split('\0')
visible = subprocess.check_output(['rg', '--files', '--hidden', '-g', '!.git/**'], cwd=root).decode().splitlines()
files = sorted({p for p in tracked + visible if p and (root / p).is_file()})
projects = []
for path in files:
    if not path.endswith('.csproj'):
        continue
    tree = ET.parse(root / path)
    items = []
    for group in tree.getroot():
        for item in group:
            if item.tag in ('PackageReference', 'ProjectReference', 'EmbeddedResource'):
                items.append({'kind': item.tag, **item.attrib, 'group_condition': group.get('Condition')})
    assets = root / Path(path).parent / 'obj/project.assets.json'
    closure = json.loads(assets.read_text()).get('libraries', {}) if assets.exists() else {}
    projects.append({'path': path, 'declarations': items,
                     'restored_libraries': {k: v['type'] for k, v in sorted(closure.items())}})
modules = []
interop = []
assets = []
for path in files:
    p = root / path
    if path.startswith('flow-lang/') and path.endswith('.flow'):
        text = p.read_text()
        modules.append({'path': path, 'imports': re.findall(r'^use\s+"([^"]+)"', text, re.M),
                        'procedure_declarations': re.findall(r'^(?:internal )?proc .*', text, re.M)})
    if path.endswith('.cs') and path.startswith(('flow-lang/', 'flow-midi/', 'flow-interpreter/', 'flow-lsp/', 'flow-cli/')):
        for n, line in enumerate(p.read_text().splitlines(), 1):
            if not line.lstrip().startswith('//') and re.search(r'DllImport\(|LibraryImport\(|NativeLibrary\.(TryLoad|Load)', line):
                interop.append({'path': path, 'line': n, 'source': line.strip()})
    if path.startswith(('flow-lang/Samples/', 'flow-lang/improv/styles/', 'flow-lang/Resources/')):
        assets.append({'path': path, 'bytes': p.stat().st_size, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()})
report = {'source_sha256': {p: hashlib.sha256((root / p).read_bytes()).hexdigest() for p in files if p.startswith(('flow-lang/', 'flow-cli/', 'flow-interpreter/', 'flow-lsp/', 'flow-midi/', 'scripts/BaselineProbe/')) and p.endswith(('.cs', '.csproj', '.flow'))}, 'projects': projects, 'modules': modules, 'native_interop_sites': interop,
          'assets': assets, 'asset_bytes': sum(a['bytes'] for a in assets),
          'note': 'Declarations retain conditions. Restored libraries reflect the most recent build, not all target closures. Native sites are a source inventory, not proof of runtime loading.'}
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(report, indent=2) + '\n')
print(f'{len(projects)} projects, {len(modules)} modules, {len(interop)} native sites, {len(assets)} assets -> {args.output}')
