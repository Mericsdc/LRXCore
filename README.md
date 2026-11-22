# LRX Core

Windows 11/10 için saf mat siyah arayüzlü format sonrası kurulum, gizlilik, hizmet yönetimi ve derin sistem temizlik aracı.

## Özellikler

- **Donanım & Sürücü Merkezi:** CPU, GPU (kayıt defteri destekli tam VRAM), RAM, Anakart ve disk bilgilerini otomatik okur; NVIDIA/AMD/Intel sürücülerini ve **Visual C++ Redistributable Runtimes (Hepsi Bir Arada / AIO)** paketini önerir.
- **Format Sonrası Toplu Kurulum:** Google Chrome, Discord, WhatsApp, Claude, ChatGPT, Spotify, Steam, Epic Games gibi uygulamaları `winget` üzerinden sessizce kurar (yönetici bağlamını reddeden kurulumlarda standart kullanıcı belirtecine otomatik geçiş yapar).
- **Gizlilik & O&O ShutUp10++ Önerileri:** Windows 11 klasik sağ tık menüsü, OneDrive komple kaldırma, telemetri, reklam kimliği, Windows Recall AI, Edge telemetri ve P2P güncelleme kapatma dahil 25 ayar; **Sadece Önerilenleri Uygula** ön ayarı ve ön yüklü UWP bloatware temizliği.
- **Güvenli Hizmet Optimizasyonu (Uyut / Yönet):** Çekirdek Windows servislerini koruma altında tutar; gereksiz/telemetri servislerini silmeden **Elle (Uyut)** moduna alır ve JSON yedek üzerinden tek tıkla geri döndürür.
- **Derin Program Kaldırıcı (HiBit Tarzı):** Önce uygulamanın kendi kaldırıcısını çalıştırır, ardından dosya, kısayol ve kayıt defteri kalıntılarını tarar.
- **Gereksiz Dosya & Kayıt Defteri Temizleyici:** Ortak Temizleyici (Windows + Tarayıcılar), Gelişmiş Temizleyici (GPU Shader, Discord/Spotify/Steam önbellekleri, >500 MB büyük dosya bulucu) ve otomatik `.reg` yedekli kayıt defteri temizleyici.
- **Uygulama & Kendi Kendini Güncelleme:** Kurulu programları `winget` üzerinden takip eder; yeni **LRX Core** sürümlerini [GitHub Releases](https://github.com/Mericsdc/LRXCore/releases) üzerinden otomatik kontrol edip kurulum sihirbazıyla günceller.

## Kurulum & Derleme

Hazır kurulum dosyasını [Releases](https://github.com/Mericsdc/LRXCore/releases) sayfasından indirip çalıştırabilirsiniz (`LRXCore-Setup-x.y.z.exe`).

Kaynaktan kurulum paketi üretmek için (.NET 10 SDK + Inno Setup 6 gerekir):

```powershell
.\scripts\build-release.ps1 -Version "1.0.0"
```
