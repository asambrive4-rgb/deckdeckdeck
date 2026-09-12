// 역할: 테스트 프로젝트의 어셈블리 메타데이터와 테스트 병렬 실행 설정을 정의합니다.
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
