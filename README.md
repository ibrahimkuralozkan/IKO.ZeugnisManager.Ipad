# IKO ZeugnisManager – plattformübergreifend

Bu repo yeni, platform bağımsız ZeugnisManager sistemidir. Eski `IKO.ZeugnisManager.Web` uygulaması çalışan yedek olarak ayrı tutulur.

## Projeler

- `ZeugnisManager.Core`: Şifreli `.zmpkg` paket biçimi ve ortak modeller.
- `ZeugnisManager.Teacher`: iPad, macOS ve Windows üzerinde çalışan, offline kurulabilir Blazor PWA.
- `ZeugnisManager.Admin`: Paket oluşturma ve sonuçları birleştirme için yönetici uygulaması.
- `ZeugnisManager.Tests`: Şifreleme, yanlış parola ve değiştirilmiş paket testleri.

Öğrenci verileri repoya ve uygulamanın statik dosyalarına eklenmez. Öğretmene yalnızca şifreli ve kapsamı sınırlandırılmış çalışma paketi verilir.

## Yerel geliştirme

```powershell
dotnet test IKO.ZeugnisManager.Ipad.sln
dotnet run --project ZeugnisManager.Teacher
```

PWA tarayıcıdan ana ekrana kurulabilir. MAUI veya sistem çapında bir runtime kurulumu gerekmez.
