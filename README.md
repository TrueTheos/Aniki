<h1 align="center">ANIKI</h1>

<p align="center">
  <img src="https://img.shields.io/github/downloads/TrueTheos/Aniki/total" alt="GitHub Downloads (all assets, all releases)">
</p>
<p align="center"><i>Streamline your anime experience, effortlessly and beautifully.</i></p>

Aniki is a cross-platform desktop application built with Avalonia UI and .NET for managing and watching your favorite anime. Featuring MyAnimeList integration, torrent search via Nyaa, and a clean, lightweight design, Aniki helps you keep track of and explore anime with ease.

<details open>
  <summary><b>Show preview</b></summary>

![UI Preview](https://i.imgur.com/LwbPosb.png)
![UI Preview](https://i.imgur.com/Xdv1ckr.png)

</details>

## Features

- **MyAnimeList Integration**: OAuth login to fetch and sync your anime list.
- **Watching Epidoes In-App**: You can browse, download and watch episodes in Aniki!
- **Anime List Management**: View, filter, and search your anime.
- **Automatic Episode Tracking**: automatically detects the anime videos you watch on your computer and synchronizes your progress.
- **Episode Search**: Search for episode and download directly from the app.
- [WIP] **Notification System**: Be automatically notified whenever a new anime or episode releases.
- [WIP] **AniList Integration**: AniList will soon be supported.

## Usage

- Download it from [Releases](https://github.com/TrueTheos/Aniki/releases)

## Self-Hosting

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download) or later
- Git

### Clone the Repository

```bash
git clone https://github.com/TrueTheos/Aniki.git
cd Aniki
```

### Setup Client ID

#### MyAnimeList:
1. Go to https://myanimelist.net/apiconfig
2. Create ID
3. Select `App Type` `other`
4. Inside `App Redirect URL` type `http://localhost:8000/callback` (you can later change it)
5. Copy the generated ClientID.
6. Replace `ClientId` inside `MalLoginProvider.cs`

#### Anilist:
1. Go to https://anilist.co/settings/developer
2. Create new client
3. Copy ID
4.  Replace `ClientId` inside `AnilistLoginProvider.cs`

### Build and Run

1. Restore dependencies:
   ```bash
   dotnet restore
   ```
2. Build the solution:
   ```bash
   dotnet build
   ```
3. Run the application:
   ```bash
   dotnet run --project Aniki/Aniki.csproj
   ```
   
## Contributing

Contributions are welcome! Please fork the repository and create a pull request with your improvements.

1. Fork the project.
2. Checkout the `develop` branch:
   ```bash
   git checkout develop
   git pull origin develop
   ```
3. Create your feature branch:
   ```bash
   git checkout -b feature/my-feature
   ```
4. Commit your changes:
   ```bash
   git commit -m "Add some feature"
   ```
5. Push to the branch:
   ```bash
   git push origin feature/my-feature
   ```
6. Open a Pull Request.

## Troubleshooting

If something doesn't work after updating to newer version, it's most likely cache issue. You can wipe cache manually by going into app installation directory. Most likely `C:\Users\[user]\AppData\Local\Aniki`.
Delete folders `tokens` `cache` and `ImageCache`.

## Acknowledgments

- Built with [Avalonia UI](https://avaloniaui.net/).

