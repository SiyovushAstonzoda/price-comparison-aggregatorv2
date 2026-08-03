function getApiBase() {
  const port = window.location.port;
  // Served by dotnet — API is on the same origin
  if (port === "5196" || port === "7249") {
    return `${window.location.origin}/api`;
  }
  // Opened via Live Server / Five Server / file preview
  return "http://localhost:5196/api";
}

export const API_BASE = getApiBase();
