# 지네 iOS build and release

Bundle ID: `com.ghtnql.zine3d`; original Unity game repository remains private. Dedicated `ghtnql/Zine-build` holds a clean snapshot without original history, Android signing files, local logs or user data. Never push `user.keystore`, `Zine.aab`, credentials or Library there.

1. Run native scene verification with the installed Unity editor and graphics (`xvfb-run` on this Linux host): `-projectPath /home/ghtnql/Zine -buildTarget iOS -executeMethod ZineNativeVerification.Run`. Review the actual PNGs under `Builds/Verification`. This exercises editor controls, not physical iPhone keyboard/audio/networking.
2. Run `ZineIOSBuild.Configure`, commit the verified code/settings, then export using `ZineIOSBuild.Export` with `ZINE_SOURCE_COMMIT` set to that exact commit. Installed editor: `/opt/Unity/2022.3.62f3/Unity`. Preserve bundled fonts; the initial bold Korean font import can take several minutes. iOS audio encoding requires ffmpeg installed on Linux.
3. Package exported `Builds/iOS` as `iOS/` with `Tools/zine-signing.py` and `Tools/zine-verify-ipa.py` as `Tools/`. SHA256 verify the archive after upload to the dedicated build snapshot's release.
4. Manually dispatch `zine-ios.yml` in `ghtnql/KKKeyboard-build`, passing the dedicated snapshot release tag and archive SHA256. The existing `testflight` environment keeps Apple signing credentials in place; they are never exported/copied to Zine's public repo. This dedicated workflow builds only Zine, creates/reuses `Zine3D-AppStore`, signs and verifies an App Store IPA. No push/PR trigger.
5. The workflow uploads to TestFlight only if the actual App Store Connect app record exists. Apple Bundle ID registration alone is insufficient. The app must be created in the website with name 지네, primary language Korean, bundle `com.ghtnql.zine3d`, SKU `zine3d-ios`.
6. Verify Apple processing and external beta review separately. No upload or compile result proves a physical-device test, an external TestFlight link or an App Store release. Prepare current iPhone screenshots, metadata, age rating and actual optional ranking privacy disclosures before review submission.
7. Download the signed artifact, verify its full SHA256 against the manifest and record source commit/version/build/signature/profile expiry. Restore the build snapshot's private visibility after the temporary build window, following the established keyboard snapshot approach. Keep original source private throughout.

Public privacy/support: https://ghtnql.github.io/Zine3D/privacy-policy.html
Ranking API: https://zine3dranking.duckdns.org
Existing ghtnqlaa Play Console listing extraction currently failed with HTTP403 under the available service account. Local images/text are references, not verified Console exports.
