# Contributing to Delta.Imaging

Thank you for your interest in contributing to Delta.Imaging! This guide explains how to contribute effectively.

## Code of Conduct

- Be respectful and inclusive
- Focus on constructive feedback
- Test your changes before submitting
- Keep discussions on-topic

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/Delta.Imaging.git
   cd Delta.Imaging
   ```
3. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```
4. **Make your changes** and commit with clear messages
5. **Push to your fork** and submit a Pull Request

## Development Workflow

### Setup

- **Visual Studio 2022** (or VS2026 for latest C#)
- **.NET 8.0 SDK** or later
- **MSYS2** with MinGW (for NBIS DLL builds)
- See [DEVELOPERS.md](DEVELOPERS.md) for detailed setup

### Building Locally

```bash
# Build C# libraries
dotnet build Delta.Wsq/

# Run tests
dotnet test Delta.Wsq/tests/

# Build NBIS DLLs (Windows only, requires MSYS2 + MSVC)
# Use GitHub Actions instead for cross-platform builds
```

### Code Style

- C# code uses **Microsoft naming conventions** (PascalCase for public members)
- 4-space indentation
- Follow [.editorconfig](.editorconfig) rules (auto-enforced by most editors)
- Use `dotnet format` to auto-fix style violations:
  ```bash
  dotnet format Delta.Wsq/
  ```

### Testing

**Add tests for:**
- New codecs or filters
- Bug fixes (write failing test first!)
- Edge cases (empty input, corrupted data, extreme dimensions)

**Run tests:**
```bash
dotnet test Delta.Wsq/tests/ --configuration Release --verbosity normal
```

**Test coverage:**
- Target: 80%+ coverage for new code
- Tools: xUnit + OpenCover (integrated in CI/CD)
- View reports: GitHub Actions > Test results

## Pull Request Guidelines

### Before Submitting

1. **Rebase on latest master**:
   ```bash
   git fetch origin
   git rebase origin/master
   ```

2. **Run all tests locally**:
   ```bash
   dotnet test Delta.Wsq/tests/ --configuration Release
   ```

3. **Check code style**:
   ```bash
   dotnet format Delta.Wsq/ --verify-no-changes
   ```

4. **Update documentation** if needed:
   - API changes → Update [README.md](README.md)
   - Implementation details → Update [DEVELOPERS.md](DEVELOPERS.md)
   - Version history → Update [CHANGELOG.md](CHANGELOG.md)

### PR Template

Use the provided [PR template](.github/PULL_REQUEST_TEMPLATE.md):

- **Title**: `<type>: <description>` (e.g., `feat: Add WebP codec`)
- **Change type**: Mark one: Feature, Fix, Enhancement, Documentation, Refactoring
- **Testing**: Describe tests added/updated
- **Breaking changes**: List any API changes
- **Checklist**: Verify all items before submitting

### What Gets Merged

✅ **Will be merged:**
- Clear commit messages
- All tests passing
- Code reviewed and approved
- Documentation updated

❌ **Will not be merged:**
- Incomplete tests
- Breaking API changes without discussion
- Windows-specific solutions (NBIS aside)
- Dependencies on unreleased libraries

## Common Scenarios

### Adding a New Codec

1. Create `Codec/*.cs` with `IImageCodec` implementation
2. Add tests in `tests/Delta.Wsq.Tests/CodecTests.cs`
3. Register in `WsqCodec` if appropriate
4. Update `CHANGELOG.md` and `README.md`

### Fixing a Bug

1. Add failing test case (reproduce the bug)
2. Fix the bug in the implementation
3. Verify test now passes
4. Update `CHANGELOG.md` with bug fix entry

### Updating NBIS

NBIS is built from: https://github.com/lessandro/nbis

If updating NBIS version:
1. Test locally with VS2022 + MinGW in MSYS2
2. Update build scripts if needed
3. Verify DLL dependencies haven't changed
4. Document version change in `DEVELOPERS.md`

## Reporting Issues

Found a bug? Please report it on [GitHub Issues](https://github.com/SlOrbA/Delta.Imaging/issues):

- **Title**: Brief description (e.g., "Decode fails on corrupted WSQ files")
- **Environment**: Windows 10/11, .NET version, Delta.Imaging version
- **Steps to reproduce**: Exact steps to trigger the bug
- **Expected vs actual**: What should happen vs what does happen
- **Attachments**: Sample files if relevant (sanitize sensitive data)

## License

By contributing, you agree that your contributions will be licensed under the [Ms-RL license](License.md).

## Questions?

- Check [DEVELOPERS.md](DEVELOPERS.md) for technical details
- Review existing issues/PRs for similar problems
- Start a GitHub Discussion for questions

Thank you for contributing! 🎉
