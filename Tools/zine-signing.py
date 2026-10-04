#!/usr/bin/env python3
"""Prepare Zine-only provisioning using existing runner credentials in place."""
import base64,json,os,plistlib,subprocess,tempfile,time,urllib.request,urllib.parse,urllib.error
from pathlib import Path
BUNDLE_ID="com.ghtnql.zine3d"
PROFILE_NAME="Zine3D-AppStore"
def _b64(data):
    return base64.urlsafe_b64encode(data).rstrip(b"=")


def _der_length(data, pos):
    value = data[pos]
    pos += 1
    if value < 128:
        return value, pos
    count = value & 127
    if not 1 <= count <= 4 or pos + count > len(data):
        raise ValueError("Invalid OpenSSL signature length")
    return int.from_bytes(data[pos:pos + count], "big"), pos + count


def _jwt():
    key = os.environ["APPSTORE_API_PRIVATE_KEY"]
    if not key or not os.environ["APPSTORE_API_KEY_ID"] or not os.environ["APPSTORE_ISSUER_ID"]:
        raise ValueError("Missing App Store Connect API credentials")
    now = int(time.time())
    header = {"alg": "ES256", "kid": os.environ["APPSTORE_API_KEY_ID"], "typ": "JWT"}
    payload = {"iss": os.environ["APPSTORE_ISSUER_ID"], "iat": now,
               "exp": now + 1140, "aud": "appstoreconnect-v1"}
    signing_input = b".".join(_b64(json.dumps(item, separators=(",", ":")).encode())
                              for item in (header, payload))
    key_path = None
    try:
        with tempfile.NamedTemporaryFile(mode="w", prefix="asc-build-key-", suffix=".p8",
                                         dir=os.environ.get("RUNNER_TEMP"), delete=False) as key_file:
            key_path = Path(key_file.name)
            key_file.write(key)
        key_path.chmod(0o600)
        der = subprocess.run(["openssl", "dgst", "-sha256", "-sign", str(key_path)],
                             input=signing_input, check=True, capture_output=True).stdout
    finally:
        if key_path is not None:
            key_path.unlink(missing_ok=True)
    if not der or der[0] != 0x30:
        raise ValueError("Invalid OpenSSL signature")
    _, pos = _der_length(der, 1)
    values = []
    for _ in range(2):
        if der[pos] != 0x02:
            raise ValueError("Invalid OpenSSL signature component")
        size, pos = _der_length(der, pos + 1)
        values.append(int.from_bytes(der[pos:pos + size], "big"))
        pos += size
    signature = b"".join(value.to_bytes(32, "big") for value in values)
    return (signing_input + b"." + _b64(signature)).decode()



def main():
    token=_jwt()
    def api(path,data=None):
        req=urllib.request.Request("https://api.appstoreconnect.apple.com/v1/"+path, headers={"Authorization":"Bearer "+token,"Content-Type":"application/json"},data=json.dumps(data).encode() if data is not None else None)
        try:
            with urllib.request.urlopen(req,timeout=45) as response:return json.load(response)
        except urllib.error.HTTPError as e:
            body=json.loads(e.read())
            raise RuntimeError("Apple API HTTP "+str(e.code)+": "+", ".join(x.get("code","")+" "+x.get("detail","") for x in body.get("errors",[]))) from None
    apps=api("apps?"+urllib.parse.urlencode({"filter[bundleId]":BUNDLE_ID,"limit":200}))["data"]
    app_id=apps[0]["id"] if apps else ""
    project=Path("export/iOS/Info.plist")
    pl=plistlib.loads(project.read_bytes())
    build=max(1,int(pl["CFBundleVersion"]))
    if app_id:
        builds=api("builds?"+urllib.parse.urlencode({"filter[app]":app_id,"limit":200}))["data"]
        while True:
            nums=[int(b["attributes"]["version"].split(".")[0]) for b in builds]
            build=max([build-1]+nums)+1
            break
    ref=Path(os.environ["RUNNER_TEMP"])/"reference.mobileprovision"
    ref.write_bytes(base64.b64decode(os.environ["IOS_APP_PROVISIONING_PROFILE_BASE64"],validate=True));ref.chmod(0o600)
    try:
        decoded=plistlib.loads(subprocess.run(["security","cms","-D","-i",str(ref)],capture_output=True,check=True).stdout)
    finally:ref.unlink(missing_ok=True)
    certs=api("certificates?limit=200")["data"]
    cert_ids=[c["id"] for c in certs if base64.b64decode(c["attributes"]["certificateContent"]) in decoded["DeveloperCertificates"]]
    if not cert_ids:raise RuntimeError("Existing signing certificate not found in Apple API")
    bundles=api("bundleIds?"+urllib.parse.urlencode({"filter[identifier]":BUNDLE_ID,"limit":200}))["data"]
    if len(bundles)!=1:raise RuntimeError("Zine bundle identifier missing or ambiguous")
    bundle=bundles[0]
    profiles=api("profiles?limit=200")["data"]
    profile=None
    for candidate in profiles:
        a=candidate["attributes"]
        if a["name"]==PROFILE_NAME and a["profileState"]=="ACTIVE" and a["profileType"]=="IOS_APP_STORE":
            linked=api("profiles/"+candidate["id"]+"/bundleId")["data"]
            cert_links=api("profiles/"+candidate["id"]+"/certificates")["data"]
            if linked["id"]==bundle["id"] and any(c["id"] in cert_ids for c in cert_links):profile=candidate;break
    if profile is None:
        profile=api("profiles",{"data":{"type":"profiles","attributes":{"name":PROFILE_NAME,"profileType":"IOS_APP_STORE"},"relationships":{"bundleId":{"data":{"id":bundle["id"],"type":"bundleIds"}},"certificates":{"data":[{"id":cert_ids[0],"type":"certificates"}]}}}})["data"]
    raw=base64.b64decode(profile["attributes"]["profileContent"])
    profiles_dir=Path.home()/"Library/MobileDevice/Provisioning Profiles";profiles_dir.mkdir(parents=True,exist_ok=True)
    dest=profiles_dir/(profile["attributes"]["uuid"]+".mobileprovision");dest.write_bytes(raw);dest.chmod(0o600)
    info=plistlib.loads(subprocess.run(["security","cms","-D","-i",str(dest)],capture_output=True,check=True).stdout)
    assert info["Entitlements"]["application-identifier"]==os.environ["APPLE_TEAM_ID"]+"."+BUNDLE_ID
    assert not info["Entitlements"].get("get-task-allow",False)
    assert "ProvisionedDevices" not in info and "ProvisionsAllDevices" not in info
    pl["CFBundleVersion"]=str(build);project.write_bytes(plistlib.dumps(pl))
    options={"method":"app-store-connect","destination":"export","signingStyle":"manual","signingCertificate":"Apple Distribution","teamID":os.environ["APPLE_TEAM_ID"],"provisioningProfiles":{BUNDLE_ID:profile["attributes"]["uuid"]},"manageAppVersionAndBuildNumber":False,"stripSwiftSymbols":True,"uploadSymbols":True}
    Path(os.environ["RUNNER_TEMP"],"ZineExportOptions.plist").write_bytes(plistlib.dumps(options))
    with open(os.environ["GITHUB_ENV"],"a") as f:
        f.write("ZINE_PROFILE_UUID="+profile["attributes"]["uuid"]+"\nZINE_BUILD_NUMBER="+str(build)+"\nZINE_APP_ID="+app_id+"\n")
    print(json.dumps({"bundleId":BUNDLE_ID,"appRecordExists":bool(app_id),"build":build,"profileName":PROFILE_NAME,"profileUUID":profile["attributes"]["uuid"]}))
if __name__=="__main__":main()
