# C#
- sealed class : 이 클래스를 부모로 해서 다른 클래스를 만들 수 없다.
- top-level statements 구조일 때, main함수는 top-level statements가 적용된 .cs파일에서 만들어짐. 그래서 top-level statements 구조를 가진 파일은 하나만 있어야 함.

- c#은 컴파일+빌드할 때, .csproj가 있는 폴더 아래에 있는 모든 .cs에 수행함. 이렇게 해서 .dll파일이 생성되고 이 단위를 assembly라고 한다.
- c#에서는 public, private 키워드를 여러 군데 적용 가능하다.

| 키워드                  | 의미                  |
| -------------------- | ------------------- |
| `public`             | 어디서든 접근 가능          |
| `private`            | 클래스 내부만             |
| `protected`          | 상속 클래스              |
| `internal`           | 같은 프로젝트(assembly)   |
| `protected internal` | 상속 + 같은 assembly    |
| `private protected`  | 상속 + 같은 assembly 내부 |

여기서 같은 프로젝트는 .csproj의 한 범위안에 있는 것을 같은 프로젝트로 본다.

- class와 struct차이
    - c++에서는 거의 둘이 비슷한데, c#에서는 완전 다름.
    - c#에서 class는 참조타입, struct는 값 타입이다.
    - 예)
    ```
    var a = new Person();
    var b = a;

    b.Age = 30;
    ```
    a ──► Person object  
    b ──► same object
    즉 여기서 a.Age와 b.Age는 둘 다 30으로 같다.
    - struct는 반대로 둘이 다른 객체로 구분된다.


- c#은 new는 있지만 delete는 없다. c++과 new의 역할은 같다. c#은 알아서 메모리를 관리한다.
    - class는 reference type이라서 생성할 때 거의 필수로 new가 필요하다.
- c#에 있는 var는 c++의 auto와 거의 같은 역할
    - 즉 초기화 반드시 필요, 컴파일 타임에 타입 결정됨.
- dynamic이라고 var처럼 타입을 추론하지만, 동적타임에 결정되는 키워드도 있음. 하지만 성능이 구려서 잘 안 쓰임.
    - 예) dynamic x = 10;
    - x = "hello"; //가능
- c#에는 nullable value type이 있음.
    - 일반적으로 null을 가질 수 없는 int, double같은 타입이 null상태를 가질 수 있도록 함.
    - 예) int? x = null;
    - default로 모든 타입이 nullable이 아닌 이유:
        - nullable이 허용되는 일반적인 value type들은 stack에 값 자체가 저장됨.
        - 하지만 null을 허용하려면 값 + null여부 두 개가 필요함.
        - 그래서 실제로 이렇게 생김. 
        ```
        struct Nullable<T>
        {
            bool HasValue;
            T Value;
        }
        ```
        - 즉 성능적으로도 손해고, 무엇보다 처음의 policy인 `value type은 값을 항상 가진다` 라는 명제를 해쳐서 default로는 잘 안 쓰임.
- c#의 .csproj에서 여러 패키지, reference project등 추가할 수 있다.
    - 여기서 reference로 추가하면 컴파일+빌드할 때, reference 프로젝트를 먼저 컴파일+빌드하고 그 뒤에 본 프로젝트에 수행함.


- c#은 빌드과정이 c++과 다름. (c#이 .NET구조에서 만들어진 언어이기 때문)
    - 맨 처음 컴파일을 바로 함. 컴파일 하고 나면 그 프로젝트에 해당되는 .dll or .exe 파일이 생성됨.
    - 이 파일들은 IL(intermediate language)로 만들어짐. 저 언어는 .NET에서 정의된 언어로 c#말고도 f#, VB.NET 등 다른 언어랑도 호환됨.
    - 저 IL로 생성된 dll파일을 보면 IL code, metadata, type info 같은 것이 다 들어있고, 그래서 .dll을 분석하면 class, method, type 정보들 다 확인 가능. 그리고 이를 이용해서 dll에서 소스코드를 찾아보는 디컴파일도 가능.
    - 저 .dll을 실행하면 실행할 때, CLR(common language runtime)이 실행됨. 즉 소스코드가 직접 쓰일 때, 한 번 더 컴파일을 함.
    - 하지만 여기서 c++처럼 모든 소스코드를 한 번에 컴파일 하지 않고, 딱 현재 쓰이는 코드만 컴파일함. 이걸 JIT(Just In Time)이라고 함.
    - 이 JIT가 실행되면 IL파일이 machine code로 변환되며 실제로 사용이 됨.
    - .exe, .dll 구성이 거의 같음. Main함수의 유무 차이
    - 기본적으로 c#에서 생성된 IL파일들(.dll, .exe)를 실행할 때는 .NET환경이 필수적임. 하지만 미리 machine code로 빌드해놓거나, 아예 CLR이 포함되게 빌드해서 필요 없게 할 수도 있음.
    - c++에서 생성되는 .exe랑 c#에서 생성되는 .exe는 내부 구조가 완전히 다르다.
- .NET 구조
```
C# / F# / VB
      ↓
Compiler (csc, fsc, vbc)
      ↓
IL (Intermediate Language)
      ↓
CLR (Common Language Runtime)
      ↓
JIT Compiler
      ↓
Machine Code
```


- c#은 파일 구조가 대략 정해져 있음.
```
using directives
↓
top-level statements
↓
type declarations (class, struct 등)
```
- c#은 컴파일할 때, 어떤 .cs를 먼저 컴파일하든지 하는 순서가 없다. 그냥 모든 .cs파일들을 SyntaxTree라는 곳에 넣고 한 번에 한다.
```
Compilation
 ├─ SyntaxTree (Program.cs)
 ├─ SyntaxTree (Instead.cs)
 └─ SyntaxTree (Utils.cs)
 ```
- c#에는 여러 기본 인터페이스들(기본 클래스 형태)를 제공한다. 그래서 사용자들은 클래스를 만들고 이를 상속하여 활용가능함.
    - 예) ILogger, IDisposable 
- Dispose() : 사용한 리소스를 명시적으로 정리하는 함수 특히 파일, 네트워크, DB connection 같은 unmanaged resource를 해제할 때
    - c#의 GC(Garbage Collector)는 메모리는 자동으로 정리하지만, 소켓, DB connection, OS handle 같은 운영체제 리소스들은 잘 처리하지 못함. 그래서 이런 거를 다루는 class에 IDisposable을 상속하고 자동으로 해당 파일이 스코프를 벗어났을 때, 리소스를 해제하게 만듦.
    ```
    var writer = new StreamWriter("test.txt");

    writer.WriteLine("hello");

    writer.Dispose();
    ```
    위에 명시적으로 적은 Dispose를 아래처럼 적으면 writer가 스코프를 벗어났을 때, 알아서 리소스가 해제됨.
    ```
    using (var writer = new StreamWriter("test.txt"))
    {
        writer.WriteLine("hello");
    }
    ```
- c#에서 using
    - namespace import : 가장 일반적인 형태, c++에서 썻던 using namespace std; 같은 거
    ```
    using System;

    Console.WriteLine("Hello");

    System.Console.WriteLine("Hello"); // using System 없으면 이렇게 써야 함.
    ```
    - disposable 처리 : c#에서 특별히 리소스 정리 패턴을 지원하기 위해 만든 기능
    ```
    using (var file = new StreamWriter("test.txt"))
    {
        file.WriteLine("hello");
    }

    or

    using var file = new StreamWriter("test.txt") // C# 8.0 버전 
    ```
    컴파일러가 알아서 using 보고 위에거를 아래거로 바꿔줌. 
    ```
    var file = new StreamWriter("test.txt");

    try
    {
        file.WriteLine("hello");
    }
    finally
    {
        file.Dispose();
    }
    ```
- c#에서 using System 에서 쓰이는 이 System은 내 컴퓨터 ProgramFile/dotnet/path 어딘가에 System.~.dll 이런 형태로 저장되어 있다.

- c#에서 readonly는 한 번 초기화되면 이후에 변경할 수 없게 하는 키워드
    - readonly var x = 10;
    - readonly vs const
    
    |           | readonly    | const              |
    | --------- | ----------- | ------------------ |
    | 변경 가능     | 생성자에서 가능    | 절대 불가              |
    | 시점        | runtime     | compile time       |
    | 타입        | 대부분 가능      | primitive / string |
    | static 여부 | instance 가능 | 항상 static          |
    - 그래서 `public static readonly DateTime StartTime = DateTime.Now;` 이렇게 동적으로 정해지는 값도 만들 수 있음.
    - 근데 참조 타입일 경우 객체 내용을 바꾸는 것은 가능
    ```
    class Test
    {
        public readonly List<int> Numbers = new List<int>();
    }

    var t = new Test();
    t.Numbers.Add(1); // ✔ 가능
    t.Numbers = new List<int>(); // ❌ 불가
    ```

- c#에서는 파일 위에 namespace (무언가); 를 적는 것만으로 그 파일에 있는 코드가 그 네임스페이스 안에 있는 걸로 침.

- c#에는 extension method라는 문법이 존재.
    ```
    namespace test

    public static class StringExtensions
    {
        public static void Print(this string text)
        {
            Console.WriteLine(text);
        }
    }
    ```
    이런식으로 사용 가능
    ```
    using test

    "hello".Print();
    
    StringExtensions.Print("hello"); //이것과 완전히 동일
    ```
    
    조건이 있음.  
    1. static class 안에 있어야 함.
    2. 메서드도 static이어야 함.
    3. 첫 번째 인자에 this + 타입 형태를 가져야 함.
    4. 사용할 때 위의 예시처럼 using test를 써야 함.
    5. 한 개의 this만 사용 가능 즉 한 개에만 붙을 수 있음. (Template 쓰면 여러 개 가능)

    - extention method에서 return을 해야 여러 개의 extension method가 붙은 chaning 구조를 쓸 수 있음.

- ?? 연산자 : null이면 오른쪽 값 사용
    ```
    a ?? b
    a가 null이 아니면 a
    a가 null이면 b
    ```

- ?. 연산자 : null이면 다음 동작 실행 안 하고, null 반환
```
// Version이 null이면 ToString() 실행 안 함.
Version?.ToString()
```

- c#에서 IConfiguration 성질을 가진 객체이면
```
configuration["SomeKey"]
```
이런식으로 사용 가능

- init, required
    - init : 객체 생성할 때만 값을 설장할 수 있고 이후에는 수정 못 하게 하는 setter
    - required : 객체 생성할 때 반드시 값을 설정해야 한다는 의미
    ```
    public required string ServiceName { get; init; } // 객체 생성 시 반드시 값을 넣어야 하고, 이후에는 변경할 수 없다.
    ```
    - readonly와의 차이점 : readonly는 변수, 즉 field에 붙을 수 있는 키워드인데(constructor에서만 설정 가능), init은 setter의 특별한 한 종류, 즉 대상이 property(객체 생성 중에만 가능) (솔직히 큰 차이 잘 모르겠음.)
    - 요즘에는 public required string ServiceName { get; init; } 이런식으로 많이 사용함.

- .NET 서버 어플리케이션 기본 구조
```
Host
 ├ Configuration
 ├ Logging
 ├ Dependency Injection(services collection)
 └ Application
```
Settle-Up 프로젝트에서 썻던 WebApplicationBuilder는 wrapper. 내부적으로는 아래처럼 작동함.

```
HostBuilder 생성
     ↓
Configuration 로드
     ↓
Logging 설정
     ↓
ServiceCollection 생성
     ↓
Kestrel 웹서버 추가
```

Configuration은 appsetting.json, environment variable등이 포함됨.

특히 저 DI에 우리가 만든 클래스들을 서비스들의 형태로 넣어서 사용하는 것
```
builder.Services.AddSingleton<BlobUploaderProvider>();
builder.Services.AddSingleton<ReceiptSessionStore>();
```
아래는 앱의 실행 순서
```
Program 시작
   ↓
WebApplication.CreateBuilder()
   ↓
Services 등록 (AddX)
   ↓
builder.Build()
   ↓
DI Container 생성
   ↓
HostedServices 실행
   ↓
app.Run()
   ↓
HTTP 요청 들어옴
   ↓
Controller 생성
   ↓
필요한 서비스 DI로 생성
```

- DI에 서비스 등록할 때, 넣을 수 있는 여러 방법이 있음. 그리고 그 방법에 따라 그 서비스가 실제로 생성되는 시점이 다름.
    - Singleton - 처음 요청될 때 생성
        - `builder.Services.AddSingleton<MyService>();`
    - Scoped - HTTP 요청마다 생성
        - `builder.Services.AddScoped<MyService>();`
    - Transient - 요청될 때마다 새로 생성
        - `builder.Services.AddTransient<MyService>();`
    - HostedService - 앱 시작할 때 바로 실행
        - `builder.services.AddHostedService<MyService>();`

- HostedService : ASP.NET Core에서 백그라운드 작업을 실행하는 서비스. 앱이 시작될 때 자동으로 실행됨.
    - StartAsync(앱 시작할 때), StopAsync(앱 종료할 때) 이 두 개의 메소드는 반드시 정의되어 있어야 함. 
- c#에는 반환값이 없는 함수를 담는 Action이라는 객체가 있음.
    - `Action a = () => Console.WriteLine("Hello");`
    - 실행할 때는 `a()`

- c#에서 람다는 쉽게 표현됨.
    - `x => x * 2` 
    위랑 아래랑 같다.
    ```
    int multiply(int x){
        return x * 2;
    }
    ```
    - 인자가 없을 시에는 `() => a` 이런식으로 사용됨.
    - c#에서 함수 객체는 Func<인자, 인자, ..., 리턴값> 이런 형태로 만들어짐.
    - 그리고 c++과 다르게 c#이 외부 변수를 캡쳐할 때는 항상 reference로 캡쳐한다. 즉
    ```
    int a = 5;

    Func<int> f = () => ++a;

    Console.WriteLine(f());
    
    //리턴값은 6, a 값도 6
    ```
    - 그래서 아래 코드에서 리턴값이 3 3 3 이 나옴.
    ```
    var actions = new List<Action>();

    for (int i = 0; i < 3; i++)
    {
        actions.Add(() => Console.WriteLine(i));
    }

    foreach (var a in actions)
    {
        a();
    }
    ```
- c#에서 쓰이는 자료구조들

| C++      | C#                        |
| -------- | ------------------------- |
| `vector` | `List<T>`                 |
| `map`    | `Dictionary<TKey,TValue>` |
| `set`    | `HashSet<T>`              |
| `queue`  | `Queue<T>`                |
| `stack`  | `Stack<T>`                |
| `deque`  | 없음 (List / LinkedList 사용) |

- c#의 DI(Dependency Injection)는 현재 등록되어 있는 서비스를 자동으로 찾아서 다른 서비스를 등록할 때, 자동으로 넣어준다. 그래서 Dependency Injection이라 불린다. 
```
    public DiscordBotWorker(
        DiscordSocketClient client,
        ILogger<DiscordBotWorker> logger,
        PingTestCommandHandler pingTestHandler,
        TestReceiptCommandHandler testReceiptHandler,
        SettleUpCommandHandler settleUpHandler,
        ReceiptInteractionService receiptInteractionService)
    {
```
위처럼 엄청나게 많은 인자가 필요하지만, 아래처럼 미리 서비스를 등록해뒀으면 DI가 자동으로 DiscordBotWorker를 만들 때, 저것들을 써서 만듦.
```
builder.Services.AddSingleton<BlobUploaderProvider>();
builder.Services.AddSingleton<ReceiptSessionStore>();
builder.Services.AddSingleton<ReceiptDraftTestDataLoader>();
builder.Services.AddSingleton<ReceiptInteractionService>();
builder.Services.AddSingleton<SettleUpCommandHandler>();
builder.Services.AddSingleton<PingTestCommandHandler>();
builder.Services.AddSingleton<TestReceiptCommandHandler>();
builder.Services.AddHostedService<DiscordBotWorker>();
```

- c#에는 string이라고 타입 이름도 있고 기본으로 정의된 클래스도 있음.
```
string.Empty

!string.IsNullOrWhiteSpace(connectionString)
```
이런 식으로 static 메소드 활용 가능

- `nameof` operator
    - 컴파일 타임 operator
    - 변수나 타입 이름을 문자열로 만들어준다.
    - `nameof(BlobImageUploader)` => `"BlobImageUploader"`

- 보통 httpclient를 추가할 때, builder.Services.AddHttpClient(); 이거를 한 다음에 httpClientFactory.CreateClient()를 이용해서 클라이언트를 만듦.
    - AddHttpClient()를 해서 그릇? 기반? 을 만들어두고, httpClientFactory.CreateClient()를 하면 DI가 자동으로 여기서 생성된 클라이언트를 그릇에 옮겨담아줌.
    - http 보내는 쪽임.

- record : 데이터 보관만을 위한 쁘띠 클래스
    ```
    // 둘 다 동일한거
    public record User(string Name, int Age);

    public class User
    {
        public string Name { get; init; }
        public int Age { get; init; }
    }
    ```
    - 값이 같으면 == 연산자에서 true로 나온다.
    
    | 타입                       | 메모리   | 특징                   |
    | ------------------------ | ----- | -------------------- |
    | `record` (record class)  | heap  | reference type       |
    | `record struct`          | stack | value type           |
    | `readonly record struct` | stack | immutable value type |

    | 특징    | record       | record struct | readonly record struct |
    | ----- | ------------ | ------------- | ---------------------- |
    | 타입    | reference    | value         | value                  |
    | 메모리   | heap         | stack         | stack                  |
    | 변경 가능 | 보통 immutable | mutable       | immutable              |
    | 복사    | reference    | value copy    | value copy             |
    | 사용 용도 | 대부분 데이터 모델   | 작은 값 객체       | 성능 중요한 값               |





# 잡다
- npm, dotnet(NuGet) 은 공개 registry로 누구나 패키지를 업로드할 수 있음. 패키지의 이름은 전세계적으로 고유해야함.

    |언어|패키지 매니저|
    |---|---|
    | JavaScript | npm            |
    | Python     | pip            |
    | C# / .NET  | NuGet          |
    | Java       | Maven / Gradle |
    | Rust       | Cargo          |
    | Go         | go modules     |
    | C++        | vcpkg / Conan  |

- npm은 소스코드 통으로 다운로드, .NET은 dll만 다운로드, .NET이 더 가벼움.

- SDK(Software Development Kit) : 어떤 플랫폼에서 프로그램을 개발하고 빌드할 수 있도록 제공되는 도구 모음
    - compiler, libraries, headers, build tools, debug tools 이것들이 포함됨.
    - 예) .NET SDK, Window SDK, Android SDK

- Framework : 프로그램의 전체 구조를 제공하는 시스템
    - library
    ```
    My Program
    ↓
    Library function
    ```
    - framework
    ```
    Framework
    ↓
    My Code
    ```
    - 예)
    ```
    app.MapGet("/", () => "Hello");
    ```
    여기서 실제 흐름은
    ```
    웹 요청
    ↓
    ASP.NET framework
    ↓
    내 handler 호출
    ```
    - 즉 이미 만들어진 프로그램 뼈대에 내 임의의 코드를 올리면서 만들 수 있는 프로그램
    - 이런거를 IoC(Inversion of Control)이라고 한단다.

- c#의 .csproj 에 있는   `<ItemGroup>    <PackageReference Include="Azure.Identity" Version="1.18.0" />` 이런 거 추가하기 전에 저거에 관련된 코드를 쓰면 무슨 타입인지 몰라서 빨간줄이 뜨는 이유 : IDE가 .csproj를 읽고 미리 프로젝트를 로드해둠. 그래서 IntelliSense 같은 거 작동되게 함. VSCode 이런 것도 해주네 ㄷㄷ;