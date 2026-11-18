# 지네 App Store preparation

This is a draft based on the inspected game, not an export of ghtnqlaa's Play Console listing. Available Play API authentication returned HTTP403. Do not label old local images as current iPhone screenshots.

- Name: 지네
- Primary language: Korean (ko)
- Bundle: com.HSV.Jine
- SKU: Zine (confirmed from App Store Connect on 2026-11-18)
- Version: 2.4.1
- Korean keywords: 지네,스네이크,뱀게임,캐주얼,아케이드,랭킹,코인,생존,zine,jine
- English keywords: centipede,snake,casual,arcade,leaderboard,coins,survival,지네,zine,jine
- Runtime supported languages: Korean (`ko`) and English (`en`); Korean devices use Korean gameplay and ranking text.
- Category proposal: Games / Casual
- Privacy and support URL: https://ghtnql.github.io/Zine3D/privacy-policy.html
- Support/report email: ghtnqlaa@naver.com

## Description draft

작은 지네를 움직여 코인을 모으고 몸을 길게 키워 보세요. 코인이 늘어날수록 움직임도 빨라집니다. 벽과 자신의 몸을 피하며 최고기록에 도전하세요.

좌우 버튼으로 간단하게 조작하고 원하는 색상으로 지네를 꾸밀 수 있습니다. 기기에 최고기록을 저장하며, 닉네임을 등록하면 공개 랭킹에 참가할 수 있습니다. 랭킹 참가는 선택 사항이며 인터넷이 없어도 게임을 즐길 수 있습니다.

게임 종료 후 Google AdMob 전면 광고가 표시될 수 있습니다. 인앱 결제는 없습니다.

## Review notes draft

Landscape iPhone game. Start with the play button; steer using the left/right controls. Ranking participation is optional. No real-name/email login. Ranking screen explains public nickname/score/time submission and provides the privacy link and deletion confirmation. Registered users can delete their own ranking identity and scores without deleting local best. The ranking API must remain reachable during review.

## Required before submission

Actual signed iPhone build and physical-device checks (controls, native keyboard, notch/home indicator, audio, interruptions/background). Current iPhone screenshots at Apple-required sizes, finalized rating answers, availability/pricing and copyright ownership details. Declare actual optional user identifier/gameplay collection and server logging, no advertising tracking. App Store Connect app record creation and external TestFlight review are separate from Bundle ID registration and IPA compilation.

## 2026-11-18 verified store state

App Store Connect app `6819955326`, bundle `com.HSV.Jine`, primary locale `ko`. Live 2.4.0 already has Korean name/description and English localization, while its binary declares English only. New 2.4.1 draft `9f61c525-fe52-4fa2-a2f5-b53fcc33138a` has both keyword lists above saved and read back. Search ranking/indexing is not guaranteed by keyword entry. Runtime Korean support and compact native ranking UI require the new binary.
