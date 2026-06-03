using System.Text.Json;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.ParserChecks;

internal static class RfsCompleteModelProfileChecks
{
   public static async Task RunAsync(List<string> failures)
   {
       await RunApplyTestAsync(failures);
       await RunApplyBalancedAsync(failures);
       await RunInitializerSeedsBalancedProfileAsync(failures);
       await RunInitializerUpgradesLegacyConfigAsync(failures);
       RunHelpPresentationAsync(failures);
       RunUnknownProfileHelpAsync(failures);
       await RunPreserveExistingFieldsAsync(failures);
       await RunPreserveUnknownFieldsAsync(failures);
       await RunUnknownProfileAsync(failures);
       await RunNoArgsAsync(failures);
       await RunModelGetAfterProfileAsync(failures);
   }

   private static Task RunApplyTestAsync(List<string> failures)
   {
       const string name = "complete profile test applies correct stage models";
       var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-complete-profile-check-{Guid.NewGuid():N}");
       try
       {
           Directory.CreateDirectory(tempDir);
           InitGitAndRfs(tempDir);

           var result = RfsCompleteModelProfileStore.SetCompleteProfile("test", tempDir);
           Expect(result.Success, $"[{name}] expected SetCompleteProfile to succeed but got: {result.ErrorMessage}.", failures);
           Expect(result.AppliedProfile is not null, $"[{name}] expected AppliedProfile to be non-null.", failures);

           var defaultModel = RckWorkspaceModelConfigStore.TryReadDefaultModel(tempDir);
           Expect(string.Equals(defaultModel, "deepseek-chat", StringComparison.Ordinal),
               $"[{name}] expected defaultModel 'deepseek-chat' but got '{defaultModel}'.", failures);

           var intentModel = RckWorkspaceModelConfigStore.TryReadStageModel("intent", tempDir);
           Expect(string.Equals(intentModel, "deepseek-chat", StringComparison.Ordinal),
               $"[{name}] expected intent 'deepseek-chat' but got '{intentModel}'.", failures);

           var proposalModel = RckWorkspaceModelConfigStore.TryReadStageModel("traceSliceProposal", tempDir);
           Expect(string.Equals(proposalModel, "deepseek-chat", StringComparison.Ordinal),
               $"[{name}] expected traceSliceProposal 'deepseek-chat' but got '{proposalModel}'.", failures);

           var memoryModel = RckWorkspaceModelConfigStore.TryReadStageModel("conversationalMemory", tempDir);
           Expect(string.Equals(memoryModel, "deepseek-chat", StringComparison.Ordinal),
               $"[{name}] expected conversationalMemory 'deepseek-chat' but got '{memoryModel}'.", failures);

           var principalAnswerModel = RckWorkspaceModelConfigStore.TryReadStageModel("principalAnswer", tempDir);
           Expect(string.Equals(principalAnswerModel, "deepseek-chat", StringComparison.Ordinal),
               $"[{name}] expected principalAnswer 'deepseek-chat' but got '{principalAnswerModel}'.", failures);
       }
       finally
       {
           SafeDeleteDirectory(tempDir);
       }

       return Task.CompletedTask;
   }

   private static Task RunApplyBalancedAsync(List<string> failures)
   {
       const string name = "complete profile balanced applies correct stage models";
       var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-complete-profile-check-{Guid.NewGuid():N}");
       try
       {
           Directory.CreateDirectory(tempDir);
           InitGitAndRfs(tempDir);

           var result = RfsCompleteModelProfileStore.SetCompleteProfile("balanced", tempDir);
           Expect(result.Success, $"[{name}] expected SetCompleteProfile to succeed but got: {result.ErrorMessage}.", failures);
           Expect(result.AppliedProfile is not null, $"[{name}] expected AppliedProfile to be non-null.", failures);

           var defaultModel = RckWorkspaceModelConfigStore.TryReadDefaultModel(tempDir);
           Expect(string.Equals(defaultModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected defaultModel 'gpt-5.4-mini' but got '{defaultModel}'.", failures);

           var intentModel = RckWorkspaceModelConfigStore.TryReadStageModel("intent", tempDir);
           Expect(string.Equals(intentModel, "claude-haiku-4.5", StringComparison.Ordinal),
               $"[{name}] expected intent 'claude-haiku-4.5' but got '{intentModel}'.", failures);

           var proposalModel = RckWorkspaceModelConfigStore.TryReadStageModel("traceSliceProposal", tempDir);
           Expect(string.Equals(proposalModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected traceSliceProposal 'gpt-5.4-mini' but got '{proposalModel}'.", failures);

           var memoryModel = RckWorkspaceModelConfigStore.TryReadStageModel("conversationalMemory", tempDir);
           Expect(string.Equals(memoryModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected conversationalMemory 'gpt-5.4-mini' but got '{memoryModel}'.", failures);

           var principalAnswerModel = RckWorkspaceModelConfigStore.TryReadStageModel("principalAnswer", tempDir);
           Expect(string.Equals(principalAnswerModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected principalAnswer 'gpt-5.4-mini' but got '{principalAnswerModel}'.", failures);
       }
       finally
       {
           SafeDeleteDirectory(tempDir);
       }

       return Task.CompletedTask;
   }

   private static Task RunInitializerSeedsBalancedProfileAsync(List<string> failures)
   {
       const string name = "initializer seeds balanced profile with principalAnswer";
       var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-complete-profile-check-{Guid.NewGuid():N}");
       try
       {
           Directory.CreateDirectory(tempDir);
           InitGitAndRfs(tempDir);

           var defaultModel = RckWorkspaceModelConfigStore.TryReadDefaultModel(tempDir);
           Expect(string.Equals(defaultModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected defaultModel 'gpt-5.4-mini' but got '{defaultModel}'.", failures);

           var intentModel = RckWorkspaceModelConfigStore.TryReadStageModel("intent", tempDir);
           Expect(string.Equals(intentModel, "claude-haiku-4.5", StringComparison.Ordinal),
               $"[{name}] expected intent 'claude-haiku-4.5' but got '{intentModel}'.", failures);

           var proposalModel = RckWorkspaceModelConfigStore.TryReadStageModel("traceSliceProposal", tempDir);
           Expect(string.Equals(proposalModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected traceSliceProposal 'gpt-5.4-mini' but got '{proposalModel}'.", failures);

           var memoryModel = RckWorkspaceModelConfigStore.TryReadStageModel("conversationalMemory", tempDir);
           Expect(string.Equals(memoryModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected conversationalMemory 'gpt-5.4-mini' but got '{memoryModel}'.", failures);

           var principalAnswerModel = RckWorkspaceModelConfigStore.TryReadStageModel("principalAnswer", tempDir);
           Expect(string.Equals(principalAnswerModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected principalAnswer 'gpt-5.4-mini' but got '{principalAnswerModel}'.", failures);
       }
       finally
       {
           SafeDeleteDirectory(tempDir);
       }

       return Task.CompletedTask;
   }

   private static Task RunInitializerUpgradesLegacyConfigAsync(List<string> failures)
   {
       const string name = "initializer upgrades legacy init config with principalAnswer";
       var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-complete-profile-check-{Guid.NewGuid():N}");
       try
       {
           Directory.CreateDirectory(tempDir);
           InitGitAndRfs(tempDir);

           var legacyConfig = new Dictionary<string, object?>
           {
               ["schemaVersion"] = 1,
               ["type"] = "rufus.workspace",
               ["createdBy"] = "rfs init",
           };
           File.WriteAllText(Path.Combine(tempDir, ".rfs", "config.json"), JsonSerializer.Serialize(legacyConfig));

           var result = RckWorkspaceInitializer.Initialize(tempDir);
           Expect(result.Success, $"[{name}] expected Initialize to succeed but got: {result.ErrorMessage}.", failures);

           var defaultModel = RckWorkspaceModelConfigStore.TryReadDefaultModel(tempDir);
           Expect(string.Equals(defaultModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected defaultModel 'gpt-5.4-mini' after upgrade but got '{defaultModel}'.", failures);

           var intentModel = RckWorkspaceModelConfigStore.TryReadStageModel("intent", tempDir);
           Expect(string.Equals(intentModel, "claude-haiku-4.5", StringComparison.Ordinal),
               $"[{name}] expected intent 'claude-haiku-4.5' after upgrade but got '{intentModel}'.", failures);

           var traceSliceProposalModel = RckWorkspaceModelConfigStore.TryReadStageModel("traceSliceProposal", tempDir);
           Expect(string.Equals(traceSliceProposalModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected traceSliceProposal 'gpt-5.4-mini' after upgrade but got '{traceSliceProposalModel}'.", failures);

           var principalAnswerModel = RckWorkspaceModelConfigStore.TryReadStageModel("principalAnswer", tempDir);
           Expect(string.Equals(principalAnswerModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected principalAnswer 'gpt-5.4-mini' after upgrade but got '{principalAnswerModel}'.", failures);
       }
       finally
       {
           SafeDeleteDirectory(tempDir);
       }

       return Task.CompletedTask;
   }

   private static Task RunPreserveExistingFieldsAsync(List<string> failures)
   {
       const string name = "complete profile preserves schemaVersion, type, createdBy";
       var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-complete-profile-check-{Guid.NewGuid():N}");
       try
       {
           Directory.CreateDirectory(tempDir);
           InitGitAndRfs(tempDir);

           // Write initial config with extra fields
           var initialConfig = new Dictionary<string, object?>
           {
               ["schemaVersion"] = 2,
               ["type"] = "rufus.workspace",
               ["createdBy"] = "rufus-test-suite",
               ["customField"] = "should-survive",
               ["llm"] = new Dictionary<string, object?>
               {
                   ["defaultModel"] = "gpt-5.4-mini",
               },
           };
           File.WriteAllText(Path.Combine(tempDir, ".rfs", "config.json"), JsonSerializer.Serialize(initialConfig));

           var result = RfsCompleteModelProfileStore.SetCompleteProfile("test", tempDir);
           Expect(result.Success, $"[{name}] expected SetCompleteProfile to succeed but got: {result.ErrorMessage}.", failures);

           // Read back raw JSON
           var rawJson = File.ReadAllText(Path.Combine(tempDir, ".rfs", "config.json"));
           using var doc = JsonDocument.Parse(rawJson);
           var root = doc.RootElement;

           Expect(root.TryGetProperty("schemaVersion", out var schemaProp),
               $"[{name}] expected schemaVersion to exist.", failures);
           Expect(schemaProp.GetInt32() == 2,
               $"[{name}] expected schemaVersion 2 but got {schemaProp}.", failures);

           Expect(root.TryGetProperty("type", out var typeProp),
               $"[{name}] expected type to exist.", failures);
           Expect(string.Equals(typeProp.GetString(), "rufus.workspace", StringComparison.Ordinal),
               $"[{name}] expected type 'rufus.workspace' but got '{typeProp.GetString()}'.", failures);

           Expect(root.TryGetProperty("createdBy", out var createdByProp),
               $"[{name}] expected createdBy to exist.", failures);
           Expect(string.Equals(createdByProp.GetString(), "rufus-test-suite", StringComparison.Ordinal),
               $"[{name}] expected createdBy 'rufus-test-suite' but got '{createdByProp.GetString()}'.", failures);

           Expect(root.TryGetProperty("customField", out var customProp),
               $"[{name}] expected customField to exist.", failures);
           Expect(string.Equals(customProp.GetString(), "should-survive", StringComparison.Ordinal),
               $"[{name}] expected customField 'should-survive' but got '{customProp.GetString()}'.", failures);
       }
       finally
       {
           SafeDeleteDirectory(tempDir);
       }

       return Task.CompletedTask;
   }

   private static Task RunPreserveUnknownFieldsAsync(List<string> failures)
   {
       const string name = "complete profile preserves unknown fields in root JSON";
       var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-complete-profile-check-{Guid.NewGuid():N}");
       try
       {
           Directory.CreateDirectory(tempDir);
           InitGitAndRfs(tempDir);

           // Write initial config with unknown fields
           var initialConfig = new Dictionary<string, object?>
           {
               ["schemaVersion"] = 1,
               ["customTool"] = "my-analyzer",
               ["llm"] = new Dictionary<string, object?>
               {
                   ["notes"] = "experimental",
               },
           };
           File.WriteAllText(Path.Combine(tempDir, ".rfs", "config.json"), JsonSerializer.Serialize(initialConfig));

           var result = RfsCompleteModelProfileStore.SetCompleteProfile("test", tempDir);
           Expect(result.Success, $"[{name}] expected SetCompleteProfile to succeed but got: {result.ErrorMessage}.", failures);

           // Read back raw JSON
           var rawJson = File.ReadAllText(Path.Combine(tempDir, ".rfs", "config.json"));
           using var doc = JsonDocument.Parse(rawJson);
           var root = doc.RootElement;

           Expect(root.TryGetProperty("customTool", out var toolProp),
               $"[{name}] expected customTool to survive.", failures);
           Expect(string.Equals(toolProp.GetString(), "my-analyzer", StringComparison.Ordinal),
               $"[{name}] expected customTool 'my-analyzer' but got '{toolProp.GetString()}'.", failures);

           Expect(root.TryGetProperty("llm", out var llmProp),
               $"[{name}] expected llm to exist.", failures);
           Expect(llmProp.TryGetProperty("notes", out var notesProp),
               $"[{name}] expected llm.notes to survive.", failures);
       }
       finally
       {
           SafeDeleteDirectory(tempDir);
       }

       return Task.CompletedTask;
   }

   private static Task RunUnknownProfileAsync(List<string> failures)
   {
       const string name = "complete profile unknown profile returns error";
       var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-complete-profile-check-{Guid.NewGuid():N}");
       try
       {
           Directory.CreateDirectory(tempDir);
           InitGitAndRfs(tempDir);

           var result = RfsCompleteModelProfileStore.SetCompleteProfile("unknown-profile", tempDir);
           Expect(!result.Success, $"[{name}] expected failure for unknown profile but got success.", failures);
           Expect(result.ErrorMessage?.Contains("Unknown Complete profile:", StringComparison.Ordinal) == true,
               $"[{name}] expected 'Unknown Complete profile' in error but got '{result.ErrorMessage}'.", failures);
       }
       finally
       {
           SafeDeleteDirectory(tempDir);
       }

       return Task.CompletedTask;
   }

   private static Task RunNoArgsAsync(List<string> failures)
   {
       const string name = "complete profile no args lists available profiles";
       var profiles = RfsCompleteModelProfileStore.GetAvailableProfiles();
       Expect(profiles.Count >= 1, $"[{name}] expected at least 1 profile.", failures);

       var testProfile = profiles.FirstOrDefault(p => string.Equals(p.Name, "test", StringComparison.Ordinal));
       Expect(testProfile is not null, $"[{name}] expected 'test' profile to exist.", failures);
       if (testProfile is not null)
       {
           Expect(string.Equals(testProfile.DefaultModel, "deepseek-chat", StringComparison.Ordinal),
               $"[{name}] expected test defaultModel 'deepseek-chat'.", failures);
           Expect(string.Equals(testProfile.IntentModel, "deepseek-chat", StringComparison.Ordinal),
               $"[{name}] expected test IntentModel 'deepseek-chat'.", failures);
       }

       var balancedProfile = profiles.FirstOrDefault(p => string.Equals(p.Name, "balanced", StringComparison.Ordinal));
       Expect(balancedProfile is not null, $"[{name}] expected 'balanced' profile to exist.", failures);
       if (balancedProfile is not null)
       {
           Expect(string.Equals(balancedProfile.DefaultModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected balanced defaultModel 'gpt-5.4-mini'.", failures);
           Expect(string.Equals(balancedProfile.IntentModel, "claude-haiku-4.5", StringComparison.Ordinal),
               $"[{name}] expected balanced IntentModel 'claude-haiku-4.5'.", failures);
           Expect(string.Equals(balancedProfile.TraceSliceProposalModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected balanced TraceSliceProposalModel 'gpt-5.4-mini'.", failures);
           Expect(string.Equals(balancedProfile.ConversationalMemoryModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected balanced ConversationalMemoryModel 'gpt-5.4-mini'.", failures);
           Expect(string.Equals(balancedProfile.PrincipalAnswerModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name}] expected balanced PrincipalAnswerModel 'gpt-5.4-mini'.", failures);
       }

       return Task.CompletedTask;
   }

   private static void RunHelpPresentationAsync(List<string> failures)
   {
       const string name = "complete profile help shows usage and examples";
       var lines = RfsCompleteModelProfileStore.GetCompleteProfileHelpLines();

       Expect(lines.Any(line => string.Equals(line, "Usage:", StringComparison.Ordinal)),
           $"[{name}] expected Usage section.", failures);
       Expect(lines.Any(line => line.Contains("/complete-profile <test|balanced>", StringComparison.Ordinal)),
           $"[{name}] expected explicit profile choices in usage.", failures);
       Expect(lines.Any(line => line.Contains("Cheap/local-oriented profile for testing Complete mode", StringComparison.Ordinal)),
           $"[{name}] expected test profile description.", failures);
       Expect(lines.Any(line => line.Contains("Balanced profile for normal Complete mode usage", StringComparison.Ordinal)),
           $"[{name}] expected balanced profile description.", failures);
       Expect(lines.Any(line => string.Equals(line.Trim(), "/complete-profile test", StringComparison.Ordinal)),
           $"[{name}] expected test example.", failures);
       Expect(lines.Any(line => string.Equals(line.Trim(), "/complete-profile balanced", StringComparison.Ordinal)),
           $"[{name}] expected balanced example.", failures);
   }

   private static void RunUnknownProfileHelpAsync(List<string> failures)
   {
       const string name = "complete profile unknown help shows valid profiles and examples";
       var lines = RfsCompleteModelProfileStore.GetUnknownProfileHelpLines("fast");

       Expect(lines.FirstOrDefault() == "Unknown Complete profile: fast",
           $"[{name}] expected unknown profile header.", failures);
       Expect(lines.Any(line => line.Trim() == "test"), $"[{name}] expected test profile listing.", failures);
       Expect(lines.Any(line => line.Trim() == "balanced"), $"[{name}] expected balanced profile listing.", failures);
       Expect(lines.Any(line => string.Equals(line.Trim(), "/complete-profile test", StringComparison.Ordinal)),
           $"[{name}] expected test example.", failures);
       Expect(lines.Any(line => string.Equals(line.Trim(), "/complete-profile balanced", StringComparison.Ordinal)),
           $"[{name}] expected balanced example.", failures);
   }

   private static Task RunModelGetAfterProfileAsync(List<string> failures)
   {
       const string name = "rfs model get works after applying a profile";
       var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-complete-profile-check-{Guid.NewGuid():N}");
       try
       {
           Directory.CreateDirectory(tempDir);
           InitGitAndRfs(tempDir);

           // Apply test
           var result = RfsCompleteModelProfileStore.SetCompleteProfile("test", tempDir);
           Expect(result.Success, $"[{name} test] expected success but got: {result.ErrorMessage}.", failures);

           var defaultModel = RckWorkspaceModelConfigStore.TryReadDefaultModel(tempDir);
           Expect(string.Equals(defaultModel, "deepseek-chat", StringComparison.Ordinal),
               $"[{name} test] expected 'deepseek-chat' but got '{defaultModel}'.", failures);

           // Apply balanced
           result = RfsCompleteModelProfileStore.SetCompleteProfile("balanced", tempDir);
           Expect(result.Success, $"[{name} balanced] expected success but got: {result.ErrorMessage}.", failures);

           defaultModel = RckWorkspaceModelConfigStore.TryReadDefaultModel(tempDir);
           Expect(string.Equals(defaultModel, "gpt-5.4-mini", StringComparison.Ordinal),
               $"[{name} balanced] expected 'gpt-5.4-mini' but got '{defaultModel}'.", failures);
       }
       finally
       {
           SafeDeleteDirectory(tempDir);
       }

       return Task.CompletedTask;
   }

   private static void InitGitAndRfs(string repoRoot)
   {
       var processStartInfo = new System.Diagnostics.ProcessStartInfo
       {
           FileName = "git",
           ArgumentList = { "init" },
           WorkingDirectory = repoRoot,
           RedirectStandardOutput = true,
           RedirectStandardError = true,
       };
       using var process = System.Diagnostics.Process.Start(processStartInfo);
       process?.WaitForExit();

       Directory.CreateDirectory(Path.Combine(repoRoot, ".rfs"));
   }

   private static void SafeDeleteDirectory(string path)
   {
       try
       {
           if (Directory.Exists(path))
           {
               Directory.Delete(path, recursive: true);
           }
       }
       catch
       {
           // best-effort cleanup
       }
   }

   private static void Expect(bool condition, string message, List<string> failures)
   {
       if (!condition)
       {
           failures.Add(message);
       }
   }
}
