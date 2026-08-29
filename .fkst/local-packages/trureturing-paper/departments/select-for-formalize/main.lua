local M = {}

M.spec = {
  consumes = { "paper_selection_authorized" },
  produces = { "formalization_request_ready" },
  stall_window = "5m",
}

local function required(value, name)
  if type(value) ~= "string" or value == "" then
    error("select-for-formalize: authorization is missing " .. name)
  end
  return value
end

local function repo_root(path)
  if type(path) ~= "string" or path == "" then
    error("select-for-formalize: missing observed path")
  end
  local normalized = path:gsub("\\", "/")
  local marker = "/inbox/research-selections/"
  local at = normalized:find(marker, 1, true)
  if not at then
    error("select-for-formalize: observed path is outside inbox/research-selections")
  end
  return normalized:sub(1, at)
end

local function run(argv)
  local result = exec_argv({ argv = argv, timeout = 300 })
  if result.exit_code ~= 0 then
    error(
      "select-for-formalize: selection CLI exit=" ..
      tostring(result.exit_code) .. " stderr=" .. tostring(result.stderr))
  end
  local ok, decoded = pcall(json.decode, result.stdout)
  if not ok or type(decoded) ~= "table" or
      decoded.schema ~= "paper-formalization-handoff.v1" then
    error("select-for-formalize: selection CLI returned invalid JSON")
  end
  return decoded
end

function pipeline(event)
  local observed = event.payload and event.payload.path or nil
  local root = repo_root(observed)
  local authorization = json.decode(file.read(observed))
  if type(authorization) ~= "table" or
      authorization.schema ~= "paper-selection-authorization.v1" then
    error("select-for-formalize: wrong authorization schema")
  end
  if authorization.approval ~= "submit-once" then
    error("select-for-formalize: explicit submit-once approval is required")
  end
  required(authorization.approved_by, "approved_by")
  required(authorization.approved_at, "approved_at")
  local authorization_id = required(
    authorization.authorization_id,
    "authorization_id")
  if #authorization_id ~= 71 or
      authorization_id:sub(1, 7) ~= "sha256:" or
      not authorization_id:sub(8):match("^[0-9a-f]+$") then
    error("select-for-formalize: authorization_id is not a sha256 reference")
  end

  local cli = root ..
    "src/Trureturing.Paper.ResearchSelection.Cli/bin/Release/net10.0/" ..
    "Trureturing.Paper.ResearchSelection.Cli.dll"
  if not file.exists(cli) then
    error("select-for-formalize: research selection CLI is not prebuilt: " .. cli)
  end

  local output = root .. "artifacts/research-selections/" ..
    authorization_id:sub(8)
  local mkdir = exec_argv({ argv = { "mkdir", "-p", output }, timeout = 30 })
  if mkdir.exit_code ~= 0 then
    error("select-for-formalize: cannot create output directory")
  end

  local selection_path = output .. "/paper-research-selection.v1.json"
  local request_path = output .. "/formalization-request.v1.json"
  local result = run({
    "dotnet", cli, "select",
    "--content", required(
      authorization.selection_content_path,
      "selection_content_path"),
    "--selection-out", selection_path,
    "--request-out", request_path,
  })

  raise("formalization_request_ready", {
    authorization_id = authorization_id,
    approved_by = authorization.approved_by,
    approved_at = authorization.approved_at,
    selection_ref = result.selection_ref,
    formalization_request_ref = result.formalization_request_ref,
    selection_path = result.selection_path,
    request_path = result.formalization_request_path,
    dedup_key = "paper-formalization-request:v1:" .. result.formalization_request_ref,
  })
end

return M
