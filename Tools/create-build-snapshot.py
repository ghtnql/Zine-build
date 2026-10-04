#!/usr/bin/env python3
"""Create a new history-free public snapshot without tracked Android signing files."""
import argparse,json,shutil,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('destination');args=p.parse_args()
root=Path(__file__).resolve().parents[1];dest=Path(args.destination).resolve()
assert not dest.exists(),'Use a new snapshot directory'
dest.mkdir(parents=True)
sha=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
tracked=subprocess.check_output(['git','ls-files','-z'],cwd=root).split(b'\0')
allowed={'Assets','Packages','ProjectSettings','Tools'}
for raw in tracked:
 if not raw:continue
 relative=Path(raw.decode());name=relative.name.lower()
 if relative.parts[0] not in allowed and str(relative) not in {'.gitignore','README.md'}:continue
 if name.endswith(('.keystore','.jks','.p12','.p8','.mobileprovision','.aab','.apk','.ipa')):continue
 if '__pycache__' in relative.parts:continue
 assert (root/relative).is_file() and not (root/relative).is_symlink(),relative
 target=dest/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(root/relative,target)
(dest/'SOURCE.json').write_text(json.dumps({'sourceCommit':sha,'bundleId':'com.ghtnql.zine3d','originalRepository':'ghtnql/Zine','snapshotHasOriginalHistory':False},indent=2)+'\n')
(dest/'README.md').write_text('# 지네 iOS build snapshot\n\nSanitized snapshot for `com.ghtnql.zine3d`. Original game history and Android signing material are excluded. Apple credentials stay in the existing private testflight environment. See `Tools/IOS-BUILD.md`.\n\nSource commit: `'+sha+'`\n')
assert not any(x.suffix.lower() in {'.keystore','.jks','.p12','.p8','.mobileprovision','.aab','.apk','.ipa'} for x in dest.rglob('*'))
print(json.dumps({'destination':str(dest),'sourceCommit':sha,'files':sum(x.is_file() for x in dest.rglob('*'))}))
