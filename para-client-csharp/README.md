![Logo](https://s3-eu-west-1.amazonaws.com/org.paraio/para.png)

# .NET Client for Para

[![NuGet version](https://badge.fury.io/nu/para_client_csharp.svg)](https://badge.fury.io/nu/para_client_csharp)
[![Join the chat at https://gitter.im/Erudika/para](https://badges.gitter.im/Erudika/para.svg)](https://gitter.im/Erudika/para?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

## What is this?

**Para** was designed as a simple and modular backend framework for object persistence and retrieval.
It helps you build applications faster by taking care of the backend. It works on three levels -
objects are stored in a NoSQL data store or any old relational database, then automatically indexed
by a search engine and finally, cached.

This is the .NET client for Para.

### Quick start

1. Use the [NuGet](https://www.nuget.org/) package manager and add `para_client_csharp` as a 
dependency to your project:

```
Install-Package para_client_csharp -Version x.y.z
```
OR
```
dotnet add package para_client_csharp --version x.y.z
```

2. Initialize the client with your access and secret API keys.
```csharp
ParaClient client = new ParaClient('ACCESS_KEY', 'SECRET_KEY');
```

## Documentation

### [Read the Docs](https://paraio.org/docs)

## Contributing

1. Fork this repository and clone the fork to your machine
2. Create a branch (`git checkout -b my-new-feature`)
3. Implement a new feature or fix a bug and add some tests
4. Commit your changes (`git commit -am 'Added a new feature'`)
5. Push the branch to **your fork** on GitHub (`git push origin my-new-feature`)
6. Create new Pull Request from your fork

For more information see [CONTRIBUTING.md](https://github.com/Erudika/para/blob/master/CONTRIBUTING.md)

## License
[Apache 2.0](LICENSE)
