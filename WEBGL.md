# WebGL 실행 가이드

## 1) Unity 모듈 설치
- Unity Hub -> Installs -> `6000.3.2f1` -> `Add modules`
- `WebGL Build Support` 설치

## 2) WebGL 빌드
```bash
./scripts/build_webgl.sh
```

성공 시 출력 경로:
- `Build/WebGL`

빌드 로그:
- `Logs/build-webgl.log`

## 3) 로컬에서 웹 플레이
```bash
./scripts/serve_webgl.sh
```

브라우저에서 접속:
- `http://localhost:8000`

## 4) 배포
- `Build/WebGL` 폴더 전체를 정적 호스팅(예: GitHub Pages, Netlify, itch.io)으로 업로드
