local M = {}
local research = require("research_core")
local agent = require("agent_runtime")

M.spec = {
  consumes = { "paper_agent_task_seen" },
  produces = { "paper_agent_task_requested" },
  stall_window = "5m",
}

local function observed_task_path(event, root)
  local path = event.payload and event.payload.path
  if type(path) ~= "string" or path == "" then
    error("register-agent-task: file-watch event is missing path")
  end
  path = path:gsub("\\", "/")
  local prefix = root .. "inbox/agent-tasks/"
  if path:sub(1, #prefix) ~= prefix then
    error("register-agent-task: observed task is outside the deployment inbox")
  end
  local name = path:sub(#prefix + 1)
  if name == ""
      or name:find("/", 1, true)
      or not name:match("^[A-Za-z0-9._-]+%.json$") then
    error("register-agent-task: observed task filename is not canonical")
  end
  return path
end

function pipeline(event)
  local root = agent.repository_root()
  local paths = research.paths(root)
  local task_path = observed_task_path(event, root)
  local registered = research.run(paths, {
    "register-task",
    "--repository-root", paths.root,
    "--task", task_path,
  }, paths.agent_cli)
  if type(registered) ~= "table"
      or registered.schema ~= "paper-agent-task-registered.v1"
      or not agent.is_sha256(registered.task_ref)
      or not agent.is_sha256(registered.theory_program_ref) then
    error("register-agent-task: Agent CLI returned an invalid registration")
  end
  raise("paper_agent_task_requested", {
    task_ref = registered.task_ref,
    paper_id = registered.paper_id,
    theory_program_ref = registered.theory_program_ref,
    phase = registered.phase,
    agent_role = registered.agent_role,
    context_mode = registered.context_mode,
    replayed = registered.replayed == true,
    dedup_key = "paper-agent-task:v1:" .. registered.task_ref,
  })
end

return M
