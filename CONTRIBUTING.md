# Contributing

Thanks for your interest in Smart Outfit Assistant!

## Development

```powershell
cd ConsoleApp2
dotnet run --urls http://localhost:5187
```

## Pull Requests

1. Keep changes focused.
2. Run `dotnet build -c Release` before submitting.
3. Do not commit personal wardrobe data or uploaded images.

## Data Privacy

Local user data is intentionally ignored by Git:

- `App_Data/*.json`
- `wwwroot/uploads/*`
- `wwwroot/generated/reference-images/*`
