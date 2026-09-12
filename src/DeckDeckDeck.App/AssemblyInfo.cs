// 역할: 프로그램 어셈블리의 기본 메타데이터와 WPF 테마 리소스 위치를 선언합니다.
using System.Runtime.CompilerServices;
using System.Windows;

[assembly: InternalsVisibleTo("DeckDeckDeck.App.Tests")]

[assembly:ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
