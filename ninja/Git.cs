using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using log4net;

namespace Zenviro.Ninja
{
    public class Git
    {
        private const string Local = "refs/heads/master";
        private const string Remote = "refs/remotes/origin/master";
        private static readonly ILog Log = LogManager.GetLogger(typeof(Git));

        private static Signature Committer
        {
            get { return new Signature(AppConfig.GitConfigName, AppConfig.GitConfigEmail, DateTimeOffset.Now); }
        }

        static readonly object GitLock = new object();
        static Git _instance;
        public static Git Instance
        {
            get
            {
                lock (GitLock)
                    return _instance ?? (_instance = new Git());
            }
        }

        static readonly object RepoLock = new object();


        public void AddChanges()
        {
            try
            {
                lock (RepoLock)
                {
                    using (var r = new Repository(AppConfig.DataDir))
                    {
                        if (r.HasUnstagedChanges())
                        {
                            r.CommitUnstagedChanges(Committer);
                            if (!string.IsNullOrWhiteSpace(AppConfig.GitRemote) && r.Network.Remotes.Any())
                            {
                                r.SyncRemoteBranch();
                                r.Network.Push(r.Head);
                                Log.Info("Configuration pushed to remote git repository.");
                            }
                        }
                    }
                }
            }
            catch (NonFastForwardException e)
            {
                Log.Warn("The remote repository is out of sync with the local repository. Changes have not been synced to remote.");
                Log.Error(e);
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        public void Clone()
        {
            try
            {
                lock (RepoLock)
                    Repository.Clone(AppConfig.GitRemote, AppConfig.DataDir);
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        public bool Pull()
        {
            bool changeDetected;
            try
            {
                lock (RepoLock)
                    using (var r = new Repository(AppConfig.DataDir))
                    {
                        r.Fetch("origin", new FetchOptions());
                        changeDetected = r.Branches[Local].Commits.All(x => x.Sha != r.Branches[Remote].Tip.Sha);
                        if (changeDetected)
                        {
                            var result = r.Merge(r.Branches[Remote].Tip, Committer);
                            Log.Info(string.Format("DataDir updated to: {0}, with merge status: {1}.", r.Branches[Local].Tip.Sha.Substring(0,7), result.Status));
                        }
                        else
                        {
                            Log.Info(string.Format("DataDir is up to date."));
                        }
                    }
            }
            catch (Exception e)
            {
                Log.Info(e);
                throw;
            }
            return changeDetected;
        }
    }

    public static class GitExtensions
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GitExtensions));
        public static void SyncRemoteBranch(this Repository repository)
        {
            var remote = repository.Network.Remotes.Any(x => x.Name == "origin")
                ? repository.Network.Remotes["origin"]
                : repository.Network.Remotes.Add("origin", AppConfig.GitRemote);
            var canonicalName = repository.Head.CanonicalName;
            repository.Branches.Update(repository.Head,
                b => b.Remote = remote.Name,
                b => b.UpstreamBranch = canonicalName);
        }

        public static bool HasUnstagedChanges(this Repository repository)
        {
            var status = repository.Index.RetrieveStatus();
            return status.Modified.Union(status.Untracked).Union(status.Missing).Any();
        }

        public static void CommitUnstagedChanges(this Repository repository, Signature committer)
        {
            var status = repository.Index.RetrieveStatus();
            var changes = new Dictionary<string, IEnumerable<StatusEntry>>
            {
                { "Untracked", status.Untracked },
                { "Modified", status.Modified },
                { "Missing", status.Missing }
            };
            foreach (var key in changes.Keys.Where(x => changes[x].Any()))
            {
                var paths = changes[key].Select(x => x.FilePath).ToArray();
                Log.Info(string.Format("{0} configuration changes discovered.", paths.Count()));

                foreach (var path in paths)
                {
                    string message;
                    repository.Index.Stage(path);
                    switch (path.Split(Path.DirectorySeparatorChar).First())
                    {
                        case "snapshot":
                            message = string.Format("{0}, {1} {2} env ({3}).",
                                Path.GetFileNameWithoutExtension(path),
                                key == "Missing" ? "removed from" : "deployed to",
                                Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path))),
                                Path.GetFileName(Path.GetDirectoryName(path)));
                            break;
                        case "config":
                            switch (path.Split(Path.DirectorySeparatorChar)[1])
                            {
                                case "path":
                                    switch (key)
                                    {
                                        case "Untracked":
                                            message = "Search path added.";
                                            break;
                                        case "Modified":
                                            message = "Search path modified.";
                                            break;
                                        default:
                                            message = "Search path removed.";
                                            break;
                                    }
                                    break;
                                default:
                                    message = "Configuration change detected.";
                                    break;
                            }
                            break;
                        default:
                            message = "Configuration change detected.";
                            break;
                    }
                    repository.Commit(message, committer);
                }
                Log.Info("Configuration changes committed to local git repository.");
            }
        }
    }
}
