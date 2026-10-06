# EF-36 — 고철 공급의 미션 수량 연결 목표

상태: 완료. 새 Run51155+기존28종584906=636061 PASS/0 FAIL, 67출력 복원·전체 원문/보호 감사 통과. 다음 EF37 문서만 준비했다.

연결: [계획](../../../Planning/MoonRabbitJunkyard/ElementFramework/stage-36-scrap-supply-mission-plan.md) · [EF-35 결과](../../../Verification/MoonRabbitJunkyard/ElementFramework/stage-35-progress.md) · [전체 명령문](../../../Commands/MoonRabbitJunkyard/ElementFramework/stage-36-command.md).

- [x] 실제 공개 Supply/Validate/런타임 구성의 호출·프로필 조회·오류 순서와 보호 명세·정상 전환 전 전체 기준을 확보했다.
- [x] 고철 고정/유지 공급의 미션 대상 선택이 같은 고철 정의의 RemovalMissionProfile을 소비하고 MissionKind.Scrap 하드코딩으로 이를 우회하지 않는다.
- [x] 기존 Supply 서명·수량 long·모든 반환 필드·고정 항목 필터·유지 조건/0 하한·Color/Mold/Recovery/미지원/null 경계 의미를 보존했다.
- [x] 같은ID 임시 대체의 공개 Supply 수량 불일치 RED→GREEN과 실제 Validate/런타임 구성 소비를 확인하고 finally 원래 정의 참조를 복원했다. 기대 수량을 강제 설정하지 않았다.
- [x] 필요한 고철 미션 프로필 누락의 ID 오류와 상태/원본/규칙·전역 난수 무변경, 불필요한 조회·새 예외 방지와 정상6종 메타데이터를 확인했다.
- [x] 정상 전체 상태/문맥/난수/피해·미션·효과/예약·취소·재선정/발전기·공급/검증/팩 전체 바이트·본체ID·버전1/50구간 및 새 Run+기존 정확한28종 실제 종료0/FAIL0과 모든 출력 복원을 확인했다.
- [x] 원본/GUID/enum/EF-05/09/11·기존 작업/work/HEAD·과거 출력/무시 Addressables/패키지/diff check를 감사하고 의도적 차이·미검증·남은 문제·금지 작업 준수를 보고했다.
- [x] 다음 한 단계 계획/목표/전체 복사용 명령문을 저장·제시하고 다음 단계 구현을 시작하지 않았다.