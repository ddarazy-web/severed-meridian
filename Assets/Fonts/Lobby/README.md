# 로비 전용 한글 폰트

- 원본: `Assets/Fonts/MoonRabbitUI-Regular.ttf`, 함께 보관한 OFL 라이선스 적용.
- 파생 이름: MoonRabbit Lobby. 원본의 예약 이름을 새 패밀리 이름으로 사용하지 않는다.
- 범위: 기본 영문·Latin-1, 한글 완성형 11,172자, 호환 자모, 일반 문장 부호.
- 원본 6,220,984 bytes → 파생 2,447,400 bytes. 게임/레벨툴 폰트와 fallback 참조를 공유하지 않는다.
- 재생성: Python과 `fonttools==4.60.1` 환경에서 `Tools/Fonts/CreateLobbyFont.py` 실행.
- 현재 등록 레벨 이름과 로비 문구의 글리프는 검증 스크립트에서 확인한다. JSON 팩 생성 시에도 레벨 이름의 글리프를 검사하여 표시 불가능한 이름의 게시를 막는다. 다른 문자권 이름을 추가하면 범위와 글리프 검사를 함께 갱신한다.
