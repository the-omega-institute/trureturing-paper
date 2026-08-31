-- FKST-native Paper agent execution boundary.
--
-- The Paper package owns the role, prompt, sandbox, timeout, and result route. The
-- engine owns the Codex child process, timeout enforcement, process reaping, and
-- reliable-delivery retry. Deterministic task/result validation remains in the
-- repository-local Paper Agent CLI.
local M = {}
local research = require("research_core")

local function text(value)
  if type(value) == "string" then return value end
  return ""
end

function M.repository_root()
  local root = text(env_read("TRURETURING_PAPER_REPOSITORY_ROOT"))
  if root == "" then
    error("paper-agent: TRURETURING_PAPER_REPOSITORY_ROOT is required")
  end
  root = root:gsub("\\", "/")
  if root:sub(-1) ~= "/" then root = root .. "/" end
  return root
end

function M.is_sha256(value)
  return type(value) == "string"
    and #value == 71
    and value:sub(1, 7) == "sha256:"
    and value:sub(8):match("^[0-9a-f]+$") ~= nil
end

function M.lock_key(task_ref)
  if not M.is_sha256(task_ref) then
    error("paper-agent: task_ref must be sha256")
  end
  return "trureturing-paper/agent-tasks/" .. task_ref:sub(8)
end

function M.validate_recorded(result)
  if type(result) ~= "table" or result.schema ~= "paper-agent-result-recorded.v1" then
    error("paper-agent: Agent CLI returned the wrong result schema")
  end
  if not M.is_sha256(result.task_ref) or not M.is_sha256(result.result_ref) then
    error("paper-agent: recorded task/result references must be sha256")
  end
  if result.status ~= "completed"
      and result.status ~= "no-progress"
      and result.status ~= "blocked" then
    error("paper-agent: unsupported recorded result status")
  end
  if type(result.outputs) ~= "table" then
    error("paper-agent: recorded outputs must be an array")
  end
  for _, output in ipairs(result.outputs) do
    if type(output) ~= "table"
        or text(output.schema) == ""
        or text(output.workspace_relative_path) == ""
        or not M.is_sha256(output.artifact_ref) then
      error("paper-agent: malformed recorded output artifact")
    end
  end
  if text(result.paper_id) == ""
      or not M.is_sha256(result.theory_program_ref)
      or text(result.phase) == ""
      or text(result.agent_role) == ""
      or text(result.context_mode) == ""
      or text(result.summary) == ""
      or text(result.next_route) == ""
      or (result.provenance ~= "produced" and result.provenance ~= "adopted") then
    error("paper-agent: recorded result is missing required identity or provenance")
  end
  return result
end

function M.result_queue(status)
  if status == "completed" then return "paper_agent_task_completed" end
  if status == "no-progress" then return "paper_agent_task_no_progress" end
  if status == "blocked" then return "paper_agent_task_blocked" end
  error("paper-agent: unsupported result status " .. tostring(status))
end

-- Execute one prepared task. deps is test-only dependency injection. Production
-- callers omit it, so the boundary calls the FKST Codex SDK directly.
function M.execute(paths, task_ref, deps)
  deps = deps or {}
  local run_cli = deps.run_cli or research.run
  local read_file = deps.read_file or file.read
  local write_file = deps.write_file or file.write
  local codex = deps.codex or function(options)
    return spawn_codex_sync(options)
  end

  local prepared = run_cli(paths, {
    "prepare-run",
    "--repository-root", paths.root,
    "--task-ref", task_ref,
  }, paths.agent_cli)
  if type(prepared) ~= "table"
      or prepared.schema ~= "paper-agent-run-prepared.v1"
      or prepared.task_ref ~= task_ref then
    error("paper-agent: Agent CLI returned an invalid prepared run")
  end
  if prepared.status == "replay" then
    prepared.schema = "paper-agent-result-recorded.v1"
    prepared.status = prepared.result_status
    prepared.result_status = nil
    prepared.replayed = true
    return M.validate_recorded(prepared)
  end
  if prepared.status ~= "ready" then
    error("paper-agent: unsupported prepared status " .. tostring(prepared.status))
  end
  if prepared.sandbox ~= "workspace-write" then
    error("paper-agent: every Paper Codex run requires the isolated workspace-write sandbox")
  end
  if type(prepared.timeout_seconds) ~= "number"
      or prepared.timeout_seconds < 60
      or prepared.timeout_seconds > 14400 then
    error("paper-agent: prepared timeout is outside the bounded policy")
  end
  if text(prepared.workspace_path) == ""
      or text(prepared.prompt_path) == ""
      or text(prepared.stdout_path) == ""
      or text(prepared.agent_role) == "" then
    error("paper-agent: prepared run is missing workspace, prompt, stdout, or role")
  end

  local prompt = read_file(prepared.prompt_path)
  if type(prompt) ~= "string" or prompt == "" then
    error("paper-agent: prepared prompt is empty")
  end
  local result = codex({
    prompt = prompt,
    worktree = prepared.workspace_path,
    role = prepared.agent_role,
    sandbox = prepared.sandbox,
    timeout = prepared.timeout_seconds,
  })
  if type(result) ~= "table" then
    error("paper-agent: FKST Codex SDK returned no result")
  end
  if result.timed_out == true then
    error("paper-agent: Codex timed out")
  end
  if result.exit_code ~= 0 then
    error(
      "paper-agent: Codex exit=" .. tostring(result.exit_code)
      .. " error_class=" .. tostring(result.error_class or ""))
  end
  if result.provenance ~= "produced" and result.provenance ~= "adopted" then
    error("paper-agent: Codex result lacks produced/adopted provenance")
  end
  if type(result.stdout) ~= "string" or result.stdout == "" then
    error("paper-agent: Codex returned empty stdout")
  end

  local stdout_path = prepared.stdout_path
  write_file(stdout_path, result.stdout)
  local recorded = run_cli(paths, {
    "record-result",
    "--repository-root", paths.root,
    "--task-ref", task_ref,
    "--stdout", stdout_path,
    "--run-id", text(result.run_id),
    "--provenance", result.provenance,
  }, paths.agent_cli)
  recorded = M.validate_recorded(recorded)
  if recorded.task_ref ~= task_ref then
    error("paper-agent: Agent CLI changed task identity")
  end
  return recorded
end

return M
