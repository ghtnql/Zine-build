#!/usr/bin/env python3
import hashlib,json,os,plistlib,subprocess,sys
from pathlib import Path
app,ipa,profile,output=map(Path,sys.argv[1:])
info=plistlib.loads((app/'Info.plist').read_bytes());signed=plistlib.loads(profile.read_bytes())
assert info['CFBundleIdentifier']=='com.ghtnql.zine3d'
assert info['CFBundleVersion']==os.environ['ZINE_BUILD_NUMBER']
assert signed['Entitlements']['application-identifier']==os.environ['APPLE_TEAM_ID']+'.com.ghtnql.zine3d'
assert signed['Entitlements'].get('get-task-allow',False) is False
assert 'ProvisionedDevices' not in signed
assert info.get('ITSAppUsesNonExemptEncryption') is False
assert info['UIDeviceFamily']==[1],info['UIDeviceFamily']
assert set(info['UISupportedInterfaceOrientations'])=={'UIInterfaceOrientationLandscapeLeft','UIInterfaceOrientationLandscapeRight'}
assert 'arm64' in subprocess.run(['lipo','-archs',str(app/info['CFBundleExecutable'])],check=True,capture_output=True,text=True).stdout
privacy=list(app.rglob('PrivacyInfo.xcprivacy'));assert privacy
assert any(x.get('NSPrivacyAccessedAPIType')=='NSPrivacyAccessedAPICategoryUserDefaults' for p in privacy for x in plistlib.loads(p.read_bytes()).get('NSPrivacyAccessedAPITypes',[]))
assert not any('Analytics' in str(p) or 'UnityAds' in str(p) for p in app.rglob('*'))
source=json.loads(Path('export/iOS/ZINE_EXPORT.json').read_text())
report={**source,'build':info['CFBundleVersion'],'sha256':hashlib.sha256(ipa.read_bytes()).hexdigest(),'bytes':ipa.stat().st_size,'appleSigned':True,'profileUUID':signed['UUID'],'profileExpires':signed['ExpirationDate'].isoformat(),'privacyManifests':[str(p.relative_to(app)) for p in privacy],'deviceTested':False}
output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
