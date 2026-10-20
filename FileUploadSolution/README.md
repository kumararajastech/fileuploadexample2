# .NET 10 File Upload API - Architecture & Deployment Guide

## Overview

This solution provides a production-grade ASP.NET Core Web API built on **.NET 10** for uploading and persisting files to network shared folders (UNC paths) or local drives. It implements Clean Architecture principles, secure streaming, path traversal protection, startup configuration validation, and centralized exception handling.

---

## Architecture & Design Highlights

1. **Clean Architecture & Separation of Concerns**:
   - `FilesController`: Handles HTTP transport, status codes, and input validation.
   - `SharedFolderStorageService`: Handles file system I/O, streaming, path validation, and security enforcement.
   - `GlobalExceptionMiddleware`: Sanitizes exceptions so internal filesystem paths are never leaked to clients.

2. **Security Features**:
   - **Path Traversal Prevention**: Resolves normalized full paths and verifies target paths remain strictly inside the configured root directory.
   - **File Renaming**: Generates GUID-based server filenames (`FileId.ext`) to avoid trusting client filenames or overwriting existing files.
   - **Streaming File Persist**: Uses asynchronous stream copying (`Stream.CopyToAsync`) with an 80 KB buffer to maintain minimal RAM overhead regardless of file size.
   - **Zero Leaks**: Absolute storage paths are excluded from API responses and log outputs.

---

## Deployment & IIS UNC Share Configuration

### 1. IIS UNC Shared Folder Requirements
When hosting under IIS on Windows Server to access UNC shares (`\\FileServer\SharedFiles`):

- **Do NOT use Mapped Drives (e.g., `Z:\SharedFiles`)**:
  Mapped drive letters are session-specific and unavailable to non-interactive Windows services like IIS Application Pools. Always use standard UNC syntax (`\\FileServer\ShareName`).

- **Application Pool Identity & Service Accounts**:
  Standard `AppPoolIdentity` or `NETWORK SERVICE` accounts usually lack access to remote network shares across Active Directory domains.
  - **Recommended**: Configure the IIS Application Pool to run under a **Group Managed Service Account (gMSA)** or a dedicated **Domain Service Account**.
  - **Permissions Required**:
    - **Share Permissions**: Change & Read (or Full Control).
    - **NTFS Permissions**: Modify, Read & Execute, List Folder Contents, Read, Write on the target folder.

- **Directory Existence Check**:
  The API validates directory existence on startup/upload. If the network share is disconnected or inaccessible due to missing permissions, the API returns a clean `503 Service Unavailable` response without hanging or leaking system diagnostics.

---

## Running Locally

### Configuration (`appsettings.json`)

```json
{
  "FileStorage": {
    "SharedFolderPath": "C:\\LocalSharedFolder",
    "MaxFileSizeInMB": 20,
    "AllowedExtensions": [
      ".pdf",
      ".docx",
      ".xlsx",
      ".csv",
      ".txt",
      ".png",
      ".jpg",
      ".jpeg"
    ]
  }
}
```

### CLI Commands

```bash
# Restore dependencies
dotnet restore FileUploadSolution.sln

# Build solution
dotnet build FileUploadSolution.sln --configuration Release

# Run unit tests
dotnet test FileUploadSolution.sln

# Launch API locally
dotnet run --project FileUpload.Api/FileUpload.Api.csproj
```

---

## Example Requests

### `curl` Example

```bash
curl -X POST "https://localhost:5001/api/files/upload" \
  -H "accept: application/json" \
  -F "file=@sample.pdf"
```

### Sample Success Response (`201 Created`)

```json
{
  "fileId": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "fileName": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855.pdf",
  "fileSizeInBytes": 125000,
  "contentType": "application/pdf",
  "uploadedAtUtc": "2026-10-19T17:22:00Z"
}
```

### Sample Error Response (`400 Bad Request`)

```json
{
  "statusCode": 400,
  "message": "File extension '.exe' is not permitted.",
  "traceId": "0HN123456789A:00000001",
  "timestampUtc": "2026-10-19T17:22:05Z"
}
```
