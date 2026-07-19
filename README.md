\# CommandAPI



A Rich HUD Master command console and command registration framework for

Space Engineers.



\## Runtime dependency



Add Rich HUD Master to the world:



\- Workshop item: `1965654081`



The Rich HUD Client and Shared source is vendored from:



\- repository: `ZachHembree/RichHudFramework.Client`

\- commit: `058a31e3431a9c0df0778770d4747992f06de175`

\- commit date: `2025-12-11`



\## Current milestone



The initial bootstrap proves:



\- Space Engineers loads the CommandAPI session component;

\- CommandAPI registers with Rich HUD Master;

\- the Rich HUD initialization callback runs;

\- the project builds with MDK2 and .NET Framework 4.8.



When registration succeeds, a temporary notification appears:



`CommandAPI connected to Rich HUD Master.`



The notification is bootstrap diagnostics and will be removed after the

integration is verified.



\## Build



From the mod root:



`dotnet build Data\\CommandAPI.csproj --nologo`

