local M = {}

function M.repo_root(observed_path, marker)
  if type(observed_path) ~= "string" or observed_path == "" then
    return nil, "missing observed path"
  end
  local normalized = observed_path:gsub("\\", "/")
  local boundary = marker or "/inbox/research-inputs/"
  local at = normalized:find(boundary, 1, true)
  if not at then
    return nil, "observed path is outside " .. boundary
  end
  return normalized:sub(1, at)
end

function M.paths(repo_root)
  return {
    root = repo_root,
    cli = repo_root .. "tools/Trureturing.Paper.ResearchInput.Cli/bin/Release/net10.0/Trureturing.Paper.ResearchInput.Cli.dll",
    selection_cli = repo_root .. "src/Trureturing.Paper.ResearchSelection.Cli/bin/Release/net10.0/Trureturing.Paper.ResearchSelection.Cli.dll",
    store = repo_root .. "artifacts/research-input",
    work = repo_root .. "work/research-input",
  }
end

function M.ensure_dir(path)
  local result = exec_argv({ argv = { "mkdir", "-p", path }, timeout = 30 })
  if result.exit_code ~= 0 then
    error("research-input mkdir failed: " .. tostring(result.stderr))
  end
end

function M.run(paths, args, cli)
  local executable = cli or paths.cli
  if not file.exists(executable) then
    error("paper repository-local CLI is not prebuilt: " .. executable)
  end
  local argv = { "dotnet", executable }
  for _, value in ipairs(args) do table.insert(argv, value) end
  local result = exec_argv({ argv = argv, timeout = 300 })
  if result.exit_code ~= 0 then
    error("paper repository-local CLI exit=" .. tostring(result.exit_code) .. " stderr=" .. tostring(result.stderr))
  end
  local ok, decoded = pcall(json.decode, result.stdout)
  if not ok or type(decoded) ~= "table" then
    error("paper repository-local CLI returned invalid JSON")
  end
  return decoded
end

function M.read_envelope(path, expected_schema)
  local value = json.decode(file.read(path))
  if type(value) ~= "table" or value.schema ~= expected_schema then
    error("paper research-input envelope has wrong schema")
  end
  return value
end

function M.required(value, name)
  if type(value) ~= "string" or value == "" then
    error("paper research-input envelope is missing " .. name)
  end
  return value
end

return M
