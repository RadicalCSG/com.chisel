"""Checks a Unity package without Unity: meta files, their GUIDs, package.json and the issue templates.

    python .github/scripts/check_package.py [package folder]
"""
import json, os, re, sys

root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '..', '..'))
errors = []

def error(message):
    errors.append(message)
    print('::error::' + message if os.environ.get('GITHUB_ACTIONS') else 'ERROR: ' + message)

# Unity skips hidden files and folders, and folders ending in ~
def hidden(name):
    return name.startswith('.') or name.endswith('~')

def relative(path):
    return os.path.relpath(path, root).replace(os.sep, '/')

# Every asset needs its .meta, every .meta its asset, and no two .meta files may share a GUID
guids = {}
for folder, folders, files in os.walk(root):
    folders[:] = [f for f in folders if not hidden(f)]
    names = set(folders) | set(files)
    for name in folders + [f for f in files if not f.endswith('.meta')]:
        if hidden(name):
            continue
        if name + '.meta' not in names:
            error(f'{relative(os.path.join(folder, name))} has no .meta file')
    for name in files:
        if not name.endswith('.meta') or hidden(name):
            continue
        path = os.path.join(folder, name)
        if name[:-len('.meta')] not in names:
            error(f'{relative(path)} has no asset next to it')
        text = open(path, encoding='utf-8', errors='replace').read()
        match = re.search(r'^guid: *([0-9a-f]{32})\s*$', text, re.MULTILINE)
        if not match:
            error(f'{relative(path)} has no guid')
            continue
        guid = match.group(1)
        if guid in guids:
            error(f'{relative(path)} has the same guid as {guids[guid]}')
        else:
            guids[guid] = relative(path)

# package.json
try:
    package = json.load(open(os.path.join(root, 'package.json'), encoding='utf-8'))
    for key in ('name', 'version', 'displayName', 'unity', 'description'):
        if not package.get(key):
            error(f'package.json has no "{key}"')
    if not re.fullmatch(r'\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?', package.get('version', '')):
        error(f'package.json\'s version "{package.get("version")}" is not a semantic version')
    if not re.fullmatch(r'\d{4}\.\d+', package.get('unity', '')):
        error(f'package.json\'s unity "{package.get("unity")}" is not a Unity version such as 6000.6')
    for name, version in package.get('dependencies', {}).items():
        if not re.fullmatch(r'\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?', version):
            error(f'package.json\'s dependency {name} has no exact version: "{version}"')
    for sample in package.get('samples', []):
        if not os.path.isdir(os.path.join(root, sample.get('path', ''))):
            error(f'package.json\'s sample "{sample.get("displayName")}" has no folder {sample.get("path")}')
except (OSError, ValueError) as e:
    error(f'package.json can\'t be read: {e}')

# Issue templates
try:
    import yaml
    templates = os.path.join(root, '.github', 'ISSUE_TEMPLATE')
    for name in sorted(os.listdir(templates)) if os.path.isdir(templates) else []:
        if not name.endswith(('.yml', '.yaml')):
            continue
        try:
            template = yaml.safe_load(open(os.path.join(templates, name), encoding='utf-8'))
        except yaml.YAMLError as e:
            error(f'.github/ISSUE_TEMPLATE/{name} is not valid YAML: {e}')
            continue
        if name.startswith('config.'):
            continue
        for key in ('name', 'description', 'body'):
            if not template.get(key):
                error(f'.github/ISSUE_TEMPLATE/{name} has no "{key}"')
        ids = [item.get('id') for item in template.get('body', []) if item.get('id')]
        if len(ids) != len(set(ids)):
            error(f'.github/ISSUE_TEMPLATE/{name} uses an id twice')
except ImportError:
    print('PyYAML is not installed: the issue templates are not checked')

print(f'{len(guids)} meta files checked, {len(errors)} problem(s)')
sys.exit(1 if errors else 0)
