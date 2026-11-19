# Claude Project Handoff — IKO ZeugnisManager iPad/PWA

Bu belge yeni bir Claude oturumunun projeyi hızlı ve doğru şekilde devralması içindir. Önce bu dosyayı, ardından `README.md` ve aşağıda belirtilen kaynak dosyaları oku.

## 1. Projenin amacı

Bodelschwinghschule öğretmenlerinin karne değerlendirmelerini iPad, Windows veya macOS üzerinde girebildiği; öğrenci verilerini merkezi bir web sunucusunda tutmadan şifreli dosyalarla yöneticiden öğretmene ve tekrar yöneticiye aktarabildiği platform bağımsız bir sistem geliştiriliyor.

Temel iş akışı:

1. Yönetici Windows uygulamasında öğretmen/ders kapsamına özel bir `.zmpkg` çalışma paketi oluşturur.
2. Paket LOGINEO gibi okulun onayladığı bir kanal üzerinden öğretmene gönderilir.
3. Öğretmen iPad PWA'da paketi parola ile açar ve yalnızca yetkili alanları doldurur.
4. PWA şifreli bir `Rueckgabe_*.zmpkg` üretir.
5. Yönetici dönüş paketini doğrular ve sonuçları merkezi verilere aktarır.

Uygulamanın yayınlanan boş PWA kopyasında öğrenci, öğretmen, karne, parola veya `.zmpkg` dosyası bulunmamalıdır.

## 2. Repolar ve bağlantılar

Yeni platform bağımsız repo:

- GitHub: https://github.com/ibrahimkuralozkan/IKO.ZeugnisManager.Ipad
- Canlı öğretmen PWA: https://ibrahimkuralozkan.github.io/IKO.ZeugnisManager.Ipad/
- GitHub Actions: https://github.com/ibrahimkuralozkan/IKO.ZeugnisManager.Ipad/actions
- Yerel konum: `C:\Users\b230650\source\repos\ibrahimkuralozkan\IKO.ZeugnisManager.Ipad`
- Solution: `IKO.ZeugnisManager.Ipad.sln`

Eski çalışan karne uygulaması/yedek:

- GitHub: https://github.com/ibrahimkuralozkan/IKO.ZeugnisManager.Web
- Yerel konum: `C:\Users\b230650\source\repos\ibrahimkuralozkan\IKO.ZeugnisManager.Web`

Eski Web uygulaması öğretmenlerin mevcut çalışan sistemi olarak korunmalıdır. Yeni yönetici akışı tamamen doğrulanmadan eski repo silinmemeli veya geri döndürülemez şekilde değiştirilmemelidir.

## 3. Solution mimarisi

### `ZeugnisManager.Core`

Platformdan bağımsız ortak kod:

- `ExchangePackage.cs`: Paket sözleşmeleri, öğrenci kayıtları, değerlendirme alanları.
- `PackageCrypto.cs`: `.zmpkg` şifreleme/şifre çözme.
- `PackageResultFactory.cs`: Assignment paketinden Result paketi üretme.

### `ZeugnisManager.Teacher`

Blazor WebAssembly PWA. iPad Safari, Windows ve macOS tarayıcılarında çalışır.

- Ana ekran: `Pages/Home.razor`
- PWA manifest: `wwwroot/manifest.webmanifest`
- Service worker: `wwwroot/service-worker.published.js`
- Dosya indirme JavaScript'i: `wwwroot/index.html`

PWA GitHub Pages üzerinden yayınlanır. `main` dalında `Teacher` veya `Core` değişirse `.github/workflows/deploy-teacher-pwa.yml` otomatik build/deploy yapar. Workflow, Pages için `<base href="/IKO.ZeugnisManager.Ipad/" />` değerini yalnız yayın derlemesinde uygular; yerel Visual Studio çalışması `/` kullanmaya devam eder.

### `ZeugnisManager.Admin`

Blazor Server yönetici uygulamasının temeli.

- Pilot ekran: `Components/Pages/Pilot.razor`
- Pilot paket servisi: `Services/PilotPackageService.cs`
- Pilot yetki kayıtları yalnız yerelde `AppData/Pilot/authorizations.json` altında tutulur ve `.gitignore` ile dışlanır.

Pilot ekran iki yapay öğrenci (`Test Schüler A/B`) ile uçtan uca iPad testini doğrulamak içindir. Gerçek öğrenci verisi içermez.

v0.4.0 ile gerçek yerel paket akışı eklendi:

- Veri kaynağı seçimi: `Components/Pages/Datenquelle.razor`
- Gerçek paket oluşturma ve içe aktarma: `Components/Pages/Pakete.razor`
- Yerel veri bağlantısı: `Services/LegacyDataSourceService.cs`
- Kapsam, yetkilendirme, replay koruması ve audit: `Services/RealPackageService.cs`
- Paket yetki/audit kayıtları: yalnız yerel `ZeugnisManager.Admin/AppData/Exchange` (Git dışında)

v0.5.0 ile taşınabilir Admin akışı eklendi:

- Okul bilgisayarında şifreli çalışma dosyası: `Components/Pages/MobilAdmin.razor` (`/mobiladmin`)
- Tablet PWA Admin modu: `ZeugnisManager.Teacher/Pages/PortableAdmin.razor` (`/admin`)
- Ortak `.zmadm` modeli ve AES-GCM şifreleme: `PortableAdminWorkspace.cs`, `WorkspaceCrypto.cs`
- Tarayıcı Web Crypto uygulaması: `BrowserWorkspaceService.cs`, `workspaceCrypto.js`
- `.zmadm` yalnız okulun LOGINEO bulutunda tutulur; parola aynı bulutta saklanmaz.
- Her kayıtta revizyon artar. Aynı çalışma dosyası iki cihazda eşzamanlı düzenlenmemelidir.

### `ZeugnisManager.Tests`

`PackageCryptoTests.cs` şunları test eder:

- Doğru parola ile round-trip.
- Yanlış parolanın reddedilmesi.
- Değiştirilmiş paketin reddedilmesi.
- Result paketinin özgün Assignment paketine bağlanması.

Son durum: 4/4 test başarılı; tüm solution Release build'i 0 hata/0 uyarı.

## 4. Güvenlik modeli

`.zmpkg` güvenliği:

- AES-256-GCM authenticated encryption.
- PBKDF2-SHA256, 310.000 iteration.
- 16 byte salt, 12 byte nonce, 16 byte authentication tag, 32 byte key.
- Minimum paket parolası: 12 karakter.
- Yanlış parola veya değiştirilmiş ciphertext reddedilir.
- Paket formatı, zorunlu alanları ve son kullanma tarihi doğrulanır.
- Assignment ve Result paketlerinin ayrı `PackageId` değerleri vardır.
- Result paketinde `SourcePackageId`, özgün Assignment paketini gösterir.
- Admin pilot servisi yalnız bu cihazda oluşturulmuş ve süresi geçmemiş kaynak paketleri kabul eder.
- Aynı Result paketi ikinci kez kabul edilmez.

Önemli: Parolayı `.zmpkg` dosyasıyla aynı mesajda göndermemek gerekir. Gerçek kullanımda dosya LOGINEO üzerinden, parola ayrı bir kanal üzerinden verilmelidir.

Git'e veya GitHub Pages'e kesinlikle eklenmemesi gerekenler:

- Gerçek öğrenci ve öğretmen isimleri.
- Karneler ve değerlendirmeler.
- Paket parolaları.
- `.zmpkg` dosyaları.
- `AppData/` runtime kayıtları.

`.gitignore` bunları dışlar; her commit öncesi ayrıca kontrol et.

## 5. Yerel çalıştırma

Öğretmen PWA:

```powershell
dotnet run --project ZeugnisManager.Teacher\ZeugnisManager.Teacher.csproj --launch-profile http
```

Varsayılan adres: `http://localhost:5253`

Admin uygulaması:

```powershell
dotnet run --project ZeugnisManager.Admin\ZeugnisManager.Admin.csproj --launch-profile http
```

Pilot ekran: `http://localhost:5012/pilot`

Gerçek paket ekranı: `http://localhost:5012/pakete`

Mobil Admin dışa/içe aktarma: `http://localhost:5012/mobiladmin`

Yerel PWA mobil Admin: `http://localhost:5253/admin`

Test ve build:

```powershell
dotnet test IKO.ZeugnisManager.Ipad.sln --configuration Release
dotnet build IKO.ZeugnisManager.Ipad.sln --configuration Release
```

Visual Studio'da `Core` çalıştırılabilir proje değildir. Öğretmen uygulaması için `ZeugnisManager.Teacher`, yönetici için `ZeugnisManager.Admin` startup project seçilmelidir.

Kullanıcının Windows bilgisayarında admin yetkisi yoktur. Sistem çapında workload/runtime kurulumu gerektiren çözümlere dayanma. MAUI workload kurulumu bu nedenle iptal edildi. PWA yaklaşımı bilinçli tercihtir.

## 6. PWA yayınlama

GitHub Pages repo ayarlarında Source değeri `GitHub Actions` olarak etkinleştirilmiştir.

Her uygun `main` push'unda:

1. GitHub runner .NET 9 kurar.
2. Teacher PWA'yı Release olarak publish eder.
3. `404.html` SPA fallback ve `.nojekyll` ekler.
4. Pages artifact yükler.
5. Canlı siteyi deploy eder.

Yayın sonrası şu adresi HTTP 200 ve doğru base path için doğrula:

https://ibrahimkuralozkan.github.io/IKO.ZeugnisManager.Ipad/

## 7. Tamamlanan commitler

- `2979e9a` — İlk cross-platform Core, Teacher PWA, Admin ve Tests solution yapısı.
- `2ee27c2` — GitHub Pages otomatik Teacher PWA yayını.
- `b693d4f` — Uçtan uca yapay verili iPad pilot paket akışı.

Bu SHA'lar bağlam içindir; çalışmaya başlamadan önce güncel `git log`, `git status` ve remote durumunu kontrol et.

## 8. Şu an yapılması gereken gerçek pilot

1. Windows Admin `/pilot` ekranında en az 12 karakterli test parolasıyla paket oluştur.
2. Oluşan `Pilot_Sport_*.zmpkg` dosyasını iPad'e aktar.
3. Canlı PWA'da dosyayı ve parolayı aç.
4. Test Schüler A/B değerlendirmelerini ve Bemerkung alanını doldur.
5. `Rueckgabe_*.zmpkg` indir.
6. Dosyayı Windows Admin `/pilot` ekranına getir ve aynı parola ile doğrula.
7. Sonuçların Admin ekranında görünmesini doğrula.
8. Safari/iPad üzerinde bir hata olursa tarayıcı konsolu, ekran görüntüsü ve tam kullanıcı adımıyla teşhis et.

## 9. Sonraki geliştirme öncelikleri

Pilot başarıyla tamamlandıktan sonra:

1. Eski Web uygulamasındaki gerçek yönetici paket oluşturma mantığını yeni `ZeugnisManager.Admin` projesine kontrollü olarak taşı.
2. Gerçek öğrenci verisini GitHub'a kopyalama. Admin'in yerel kullanıcı seçimiyle eski uygulamanın veri klasörünü okumasını veya güvenli bir import mekanizmasını tercih et.
3. Ders/öğretmen kapsam manifestini Admin tarafındaki yerel yetki kaydına bağla. Result içindeki izin listesine tek başına güvenme.
4. Import sırasında yalnız kayıtlı öğrenci ID'leri ve kayıtlı rating/text/selection anahtarlarını birleştir.
5. Replay protection, expiry ve audit log'u üretim Admin servisinde koru.
6. ~~PWA'da yeni sürüm bildirimi ve kullanıcı kontrollü güncelleme ekle.~~ v0.5.3 ile tamamlandı (bkz. bölüm 13).
7. iPad Files/LOGINEO indirme-yükleme davranışını gerçek cihazlarda test et.
8. ~~Paket kapatıldığında hassas state'i bellekten temizleme ve otomatik oturum zaman aşımı ekle.~~ v0.5.2 ile tamamlandı (bkz. bölüm 12).
9. Almanca kullanıcı metinlerini sadeleştir ve öğretmen pilot geri bildirimlerine göre tablet arayüzünü iyileştir.

## 10. Çalışma kuralları

- Kullanıcının gerçek öğrenci verisini veya paketlerini izinsiz Git'e koyma.
- Her değişiklikten önce `git status`; sonrasında build/test ve `git diff --check` çalıştır.
- Eski Web reposunu çalışan yedek olarak koru.
- Geri döndürülemez Git işlemleri (`reset --hard`, history rewrite, force push) kullanma.
- GitHub Pages workflow sonucunu push sonrasında kontrol et.
- Her işlevsel değişiklikte kökteki `Directory.Build.props` sürümünü semantik olarak artır; Teacher ve Admin aynı merkezi sürümü kullanır.
- Güvenlik veya Datenschutz için kesin hukuki garanti verme; teknik önlemleri açıkça ayır.
- İletişim dili kullanıcıyla Türkçe, uygulama arayüzü öğretmenler için Almancadır.

## Claude'a başlangıç komutu

Yeni Claude oturumuna şu metin verilebilir:

> `CLAUDE_HANDOFF.md` dosyasını tamamen oku. Ardından README, solution yapısı, git status ve son commitleri incele. Eski Web repo çalışan yedektir; gerçek kişisel verileri Git'e ekleme. Önce mevcut iPad uçtan uca pilotunu doğrula, sonra handoff'taki öncelik sırasına göre yeni Admin entegrasyonuna devam et. Her değişiklikte tüm solution build'i, güvenlik testleri ve GitHub Pages deployment kontrolü yap.

## 11. Güncel kontrol noktası (19.11.2026)

Aktif sürüm `0.5.1`, son işlevsel commit `24e2e4d`.

Tamamlanan gerçek uçtan uca test adımları:

1. Okul bilgisayarındaki Admin `/mobiladmin` ekranından gerçek yerel veriler kullanılarak Revision 1 şifreli `.zmadm` çalışma dosyası oluşturuldu.
2. Yerel Teacher PWA `/admin` ekranında bu dosya başarıyla açıldı; Schuljahr, Halbjahr ve Revision doğru göründü.
3. Bir sınıf, öğretmen ve ders alanı seçilerek gerçek kapsamlı `.zmpkg` öğretmen paketi oluşturuldu.
4. Aynı işlem sonunda yetkilendirmeyi içeren Revision 2 `.zmadm` dosyası da başarıyla indirildi.
5. Şimdi Teacher PWA'nın `Lehrerpaket` ekranı açık. Son oluşturulan gerçek `.zmpkg` henüz seçilmedi/açılmadı.

Claude'un bir sonraki adımları, kullanıcıyı her mesajda yalnız tek işlemle yönlendirerek:

1. `Downloads` içindeki en yeni, pilot olmayan `.zmpkg` dosyasını `Lehrerpaket` ekranında seçtir.
2. Kullanıcının bu test için belirlediği paket parolasını girdir; parola repoya veya sohbetteki kalıcı belgeye yazılmamalı.
3. `Arbeitsauftrag öffnen` ile alanların yalnız seçilen sınıf/ders kapsamını içerdiğini doğrula.
4. Birkaç yapay test değerlendirmesi girip şifreli `Rueckgabe_*.zmpkg` üret.
5. `Mobiler Admin` ekranında mutlaka Revision 2 `.zmadm` dosyasını aç; Revision 1 kullanılmamalı çünkü paket yetkilendirmesi R2 içindedir.
6. Dönüş paketini içe aktar ve Revision 3 `.zmadm` dosyasını oluştur.
7. Okul bilgisayarındaki `/mobiladmin` ekranında R3 dosyasını önce kontrol et, sonra merkezi yerel verilere aktar. Gerçek veriyi değiştirmeden önce kullanıcıya test girdilerinin merkezi kayda yazılacağını açıkça bildir.

Önemli yerel çalışma notu: localhost PWA `%97` seviyesinde kalırsa çalışan Teacher sürecini durdur, `dotnet build ZeugnisManager.Teacher/ZeugnisManager.Teacher.csproj --configuration Debug` çalıştır ve ardından `dotnet run ... --no-build` ile yeniden başlat. Bu hata daha önce Release/Debug statik asset manifestlerinin karışmasından kaynaklandı; kod veya parola hatası değildi.

## 12. v0.5.2 — Hareketsizlik zaman aşımı (19.11.2026, ev bilgisayarı)

- Teacher PWA `Lehrerpaket` (`/`) ve `Mobiler Admin` (`/admin`) ekranlarında dosya açıkken 20 dakika hiç giriş yapılmazsa paket/çalışma dosyası, parola ve dosya baytları bellekten silinir.
- 18. dakikada uyarı gösterilir; herhangi bir dokunma/tuş/kaydırma veya `Weiterarbeiten` sayacı sıfırlar.
- Süre gerçek saatle ölçülür; iPad uykudan uyanınca süre dolmuşsa hemen kapanır.
- Kod: `ZeugnisManager.Teacher/Layout/SessionTimeout.razor`, `wwwroot/sessionTimeout.js`.
- Doğrulama: yapay test paketiyle Playwright testi (uyarı, sıfırlama, kapanış) başarılı; Release build 0 hata/0 uyarı; 7/7 test.
- Bölüm 11'deki gerçek veri adımları hâlâ okul bilgisayarında bekliyor; evde yalnız yapay verilerle çalışıldı.

## 13. v0.5.3 — Kullanıcı onaylı PWA güncellemesi (19.11.2026, ev bilgisayarı)

Ev bilgisayarındaki yerel konum: `C:\Users\ibrah\source\repos\IKO.ZeugnisManager.Ipad`.

- Önceki davranış: yeni service worker hemen `skipWaiting` yapıp tüm açık pencereleri yeniden yüklüyordu; açık paketteki kaydedilmemiş girdiler sessizce kaybolabiliyordu.
- Yeni davranış: yeni sürüm `waiting` durumunda bekler. Uygulamanın üstünde `Neue Version verfügbar` bildirimi çıkar; `Jetzt aktualisieren` veya `Später` seçilir.
- Paket veya Admin çalışma dosyası açıksa (`zmSessionTimeout.isActive()`), önce ek uyarı gösterilir; `Trotzdem aktualisieren` ile onaylanmadan yenilenmez.
- Onaydan sonra sayfa `SKIP_WAITING` mesajı gönderir; `controllerchange` olayında yalnız onay veren pencere yeniden yüklenir.
- Güncelleme kontrolü: uygulama açılışında, uygulama tekrar ön plana geldiğinde ve her 30 dakikada bir. `Später` sonrası bildirim bir sonraki açılışta tekrar görünür.
- Geçiş: v0.5.2 ve öncesi sayfalar bildirimi gösteremez. Yeni service worker kurulurken açık sayfalara `ZM_UPDATE_PING` gönderir; 2 saniye içinde yanıt yoksa eski davranış gibi kendini etkinleştirir (ama zorla yeniden yükleme yapmaz). Böylece v0.5.2 kullanıcıları v0.5.3'e takılmadan geçer.
- Kod: `wwwroot/pwaUpdate.js` (service worker kaydı da burada), `Layout/UpdateNotice.razor`, `wwwroot/service-worker.published.js`.
- Geliştirme ortamındaki `service-worker.js` önbellek kullanmadığı için bildirim yalnız publish edilmiş sürümde görülür.
- Not: `index.html` `lib/bootstrap/...` dosyasına bağlanıyor ama repoda `wwwroot/lib` yok; Bootstrap sınıfları çalışmıyor. Güncelleme bildirimi ve zaman aşımı uyarısı bu yüzden `wwwroot/css/app.css` içinde kendi stilleriyle tanımlandı.
- Doğrulama (yerel publish + Node statik sunucu, yapay veri yok): 0.5.3 ilk kurulumda bildirim yok; sahte 0.5.4 yayınında bildirim çıktı, yeni worker `waiting` kaldı; dosya açıkken ek uyarı göründü; `Jetzt aktualisieren` sonrası 0.5.4 yüklendi ve eski önbellek silindi. Geçiş testi: 0.5.2'den 0.5.3'e worker kendiliğinden etkinleşti, açık sayfa zorla yenilenmedi, sonraki açılış 0.5.3 oldu. Release build 0 hata, 7/7 test.

## 14. v0.5.4 — Yeni çalışma düzeni ve "Verwaltung" (19.11.2026, ev bilgisayarı)

Kullanıcıyla kararlaştırılan çalışma düzeni (hafta sonu/tatilde evden çalışma ihtiyacı nedeniyle):

- Günlük yönetici işi okulda da evde de yalnız PWA `/admin` sayfasında yapılır: LOGINEO'daki en yeni `.zmadm` açılır, iş yapılır, yeni revizyon LOGINEO'ya kaydedilir, eskisi silinir.
- Okul bilgisayarındaki Admin yalnız dönemde iki kez kullanılır: başta ilk `.zmadm` dosyasını oluşturmak (`/mobiladmin`), sonda son dosyayı merkezi verilere aktarmak.
- Okul Admin'indeki `/pakete` ve `/pilot` silinmedi; yedek olarak duruyor.
- Ayrı ev Admin kurulumu reddedildi: yetki/replay kayıtları bilgisayara yerel olduğu için iki Admin birbirinin paketlerini kabul etmez.

v0.5.4 değişiklikleri:

- PWA'da `Mobiler Admin` adı `Verwaltung` oldu (menü, sayfa başlığı).
- `Verwaltung` bağlantısı öğretmen menüsünde görünmez. Bir cihazda `/admin` doğrudan açıldığında `localStorage` anahtarı `zm.verwaltungDevice` kaydedilir ve o cihazda bağlantı görünür olur. Bu bir erişim kontrolü değildir, yalnız menü sadeleştirmesidir; güvenlik `.zmadm` parolasına dayanır.
- Kod: `ZeugnisManager.Teacher/Layout/NavMenu.razor`, `Pages/PortableAdmin.razor`.

Açık konular:

- `/admin` sayfasındaki metin hâlâ "Nur dienstliches Gerät" diyor; özel ev bilgisayarında kullanım için okul yönetiminin onayı kullanıcıya bırakıldı.
- Okul Admin menüsünde sayfa adı hâlâ `Mobiler Admin` (`/mobiladmin`); yeni düzende "Verwaltungsdatei" gibi bir adla sadeleştirilebilir.
- Önerilen sonraki iyileştirme: kaydederken "eski dosyayı LOGINEO'dan silin" hatırlatması.
