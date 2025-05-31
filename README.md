<h1 align="center">ANIKI</h1>

<p align="center"><i>Streamline your anime experience, effortlessly and beautifully.</i></p>

Aniki is a cross-platform desktop application built with Avalonia UI and .NET for managing and watching your favorite anime. Featuring MyAnimeList integration, torrent search via Nyaa, and a clean, lightweight design, Aniki helps you keep track of and explore anime with ease.

## Features

- **MyAnimeList Integration**: OAuth login to fetch and sync your anime list.
- **Anime List Management**: View, filter, and search your anime.
- **Automatic Episode Tracking**: automatically detects the anime videos you watch on your computer and synchronizes your progress.
- **Episode Search**: Search for episode torrents on Nyaa and download directly from the app.
- [WIP] **Notification System**: Be automatically notified whenever a new anime or episode releases.

## Usage

- Download it from [Releases](https://github.com/TrueTheos/Aniki/releases)
- or compile it yourself

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download) or later
- Git

### Clone the Repository

```bash
git clone https://github.com/TrueTheos/Aniki.git
cd Aniki
```

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

## Project Structure

```
Aniki/
├── Converters/        # Value converters for UI bindings
├── Models/            # Data models
├── Services/          # External logic and API integrations (MAL, Nyaa, OAuth)
├── ViewModels/        # MVVM ViewModels connecting Views and Models
├── Views/             # Avalonia XAML views
```

## Contributing

Contributions are welcome! Please fork the repository and create a pull request with your improvements.

1. Fork the project.
2. Create your feature branch:
   ```bash
   git checkout -b feature/my-feature
   ```
3. Commit your changes:
   ```bash
   git commit -m "Add some feature"
   ```
4. Push to the branch:
   ```bash
   git push origin feature/my-feature
   ```
5. Open a Pull Request.

## Acknowledgments

- Built with [Avalonia UI](https://avaloniaui.net/).

