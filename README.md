
# SW.HttpExtensions

[![Build and Publish NuGet Package](https://github.com/simplify9/SW-HttpExtensions/actions/workflows/nuget-publish.yml/badge.svg)](https://github.com/simplify9/SW-HttpExtensions/actions/workflows/nuget-publish.yml)
[![NuGet Version](https://img.shields.io/nuget/v/SimplyWorks.HttpExtensions.svg)](https://www.nuget.org/packages/SimplyWorks.HttpExtensions/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight .NET library that provides useful extensions for HTTP operations, including JSON serialization/deserialization, HTTP client factory helpers, and API client abstractions.

## Features

- **HttpContent Extensions**: Easy JSON deserialization from HTTP responses
- **HttpClient Extensions**: Simplified HTTP operations with automatic JSON serialization
- **HttpClientFactory Extensions**: Streamlined client creation with authentication and base address setup
- **API Client Framework**: Base classes and builders for creating structured API clients
- **JWT Token Support**: Built-in JWT authentication helpers
- **Service Collection Extensions**: Easy dependency injection setup

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package SimplyWorks.HttpExtensions
```

Or via Package Manager Console:

```powershell
Install-Package SimplyWorks.HttpExtensions
```

## Quick Start

### HttpContent Extensions

```csharp
using SW.HttpExtensions;

// Deserialize JSON response to strongly typed object
var response = await httpClient.GetAsync("api/users");
var users = await response.Content.ReadAsAsync<List<User>>();
```

### HttpClient Extensions

```csharp
using SW.HttpExtensions;

// POST with automatic JSON serialization
var user = new User { Name = "John", Email = "john@example.com" };
var response = await httpClient.PostAsync("api/users", user);

// POST with automatic deserialization
var createdUser = await httpClient.PostAsync<User>("api/users", user);

// GET with automatic deserialization
var users = await httpClient.GetAsync<List<User>>("api/users");

// PUT with automatic JSON serialization
await httpClient.PutAsync("api/users/1", user);
```

### HttpClientFactory Extensions

```csharp
using SW.HttpExtensions;

// Create client with base address and JWT authentication
var client = httpClientFactory.CreateClient(
    new Uri("https://api.example.com"), 
    jwt: "your-jwt-token"
);
```

### Service Collection Extensions

```csharp
using SW.HttpExtensions;

// Add JWT token parameters from configuration
services.AddJwtTokenParameters(options => 
{
    // Configure JWT options
});

// Add API client with options
services.AddApiClient<IUserApi, UserApiClient, UserApiMock, UserApiOptions>(options =>
{
    options.BaseUrl = "https://api.example.com";
    options.Mock = false;
});
```

## Dependencies

- .NET 8.0
- Microsoft.AspNetCore.Http.Abstractions (≥2.3.0)
- Microsoft.Extensions.Configuration.Binder (≥8.0.2)
- Microsoft.Extensions.Http (≥8.0.1)
- Newtonsoft.Json (≥13.0.3)
- SimplyWorks.PrimitiveTypes (≥8.0.0)
- System.IdentityModel.Tokens.Jwt (≥8.3.1)

## License

This project is licensed under the MIT License - see the [LICENSE](https://opensource.org/licenses/MIT) for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

If you encounter any bugs or have feature requests, please submit an [issue](https://github.com/simplify9/SW-HttpExtensions/issues).
