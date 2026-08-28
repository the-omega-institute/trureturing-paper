local M = {}

function M.repo_root(observed_path)
  if type(observed_path) ~= "string" or observed_path == "" then
    return nil, "missing observed path"
  end
  local normalized = observed_path:gsub("\\", "/")
  local marker = "/inbox/research-inputs/"
  local at = normalized:find(marker, 1, true)
  if not at then
    return nil, "observed path is outside inbox/research-inputs"
  end
  return normalized:sub(1, at)
end

function M.paths(repo_root)
  return {
    root = repo_root,
    cli = repo_root .. "tools/Trureturing.Paper.ResearchInput.Cli/bin/Release/net10.0/Trureturing.Paper.ResearchInput.Cli.dll",
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

function M.run(paths, args)
  if not file.exists(paths.cli) then
    error("paper research-input CLI is not prebuilt: " .. paths.cli)
  end
  local argv = { "dotnet", paths.cli }
  for _, value in ipairs(args) do table.insert(argv, value) end
  local result = exec_argv({ argv = argv, timeout = 300 })
  if result.exit_code ~= 0 then
    error("paper research-input CLI exit=" .. tostring(result.exit_code) .. " stderr=" .. tostring(result.stderr))
  end
  local ok, decoded = pcall(json.decode, result.stdout)
  if not ok or type(decoded) ~= "table" then
    error("paper research-input CLI returned invalid JSON")
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
