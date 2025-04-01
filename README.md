# ThreadPilot 🚀

ThreadPilot is a Windows process and power plan management tool that gives you fine-grained control over CPU affinity, process priorities, and power settings.

## Features ✨

- 💻 Process Management
  - View all running processes with real-time updates
  - Set CPU affinity for individual processes
  - Adjust process priority levels
  - Search and filter processes
  - Save/load process configurations as profiles

- ⚡ Power Plan Management  
  - View and switch between Windows power plans
  - Import custom power plan configurations
  - Monitor active power plan
  - Import custom power plan files (.pow)

## Requirements 🔧

- Windows 7 or later
- .NET 9.0 SDK
- Administrator privileges (for modifying process and power settings)

## Installation 📦

1. Clone the repository
```sh
git clone https://github.com/yourusername/ThreadPilot.git
```

2. Build the project
```sh
dotnet build
```

3. Run ThreadPilot
```sh
dotnet run
```

## Usage 🔨 

### Process Management

1. Select a process from the list
2. Modify CPU affinity by checking/unchecking CPU cores
3. Change process priority using the dropdown
4. Save configurations as profiles for quick reuse

### Power Plan Management

1. View available system and custom power plans
2. Select and activate power plans
3. Import custom power plan configurations (.pow files)

## Tech Stack 🛠️

- WPF (Windows Presentation Foundation)
- MVVM Architecture using CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- Async/Await for responsive UI
- JSON serialization for profile storage

## Contributing 🤝

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License 📄

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments 🙏

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) for MVVM implementation
- [Microsoft.Extensions.DependencyInjection](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) for DI container
