﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class UnstageChangesForAmend
    {
        public UnstageChangesForAmend(string repo, List<Models.Change> changes)
        {
            _repo = repo;

            foreach (var c in changes)
            {
                if (c.Index == Models.ChangeState.Renamed)
                {
                    _patchBuilder.Append("0 0000000000000000000000000000000000000000\t");
                    _patchBuilder.Append(c.Path);
                    _patchBuilder.Append("\0100644 ");
                    _patchBuilder.Append(c.DataForAmend.ObjectHash);
                    _patchBuilder.Append("\t");
                    _patchBuilder.Append(c.OriginalPath);
                }
                else if (c.Index == Models.ChangeState.Added)
                {
                    _patchBuilder.Append("0 0000000000000000000000000000000000000000\t");
                    _patchBuilder.Append(c.Path);
                }
                else if (c.Index == Models.ChangeState.Deleted)
                {
                    _patchBuilder.Append("100644 ");
                    _patchBuilder.Append(c.DataForAmend.ObjectHash);
                    _patchBuilder.Append("\t");
                    _patchBuilder.Append(c.Path);
                }
                else
                {
                    _patchBuilder.Append(c.DataForAmend.FileMode);
                    _patchBuilder.Append(" ");
                    _patchBuilder.Append(c.DataForAmend.ObjectHash);
                    _patchBuilder.Append("\t");
                    _patchBuilder.Append(c.Path);
                }

                _patchBuilder.Append("\n");
            }
        }

        public bool Exec()
        {
            var starter = new ProcessStartInfo();
            starter.WorkingDirectory = _repo;
            starter.FileName = Native.OS.GitExecutable;
            starter.Arguments = "-c core.editor=true update-index --index-info";
            starter.UseShellExecute = false;
            starter.CreateNoWindow = true;
            starter.WindowStyle = ProcessWindowStyle.Hidden;
            starter.RedirectStandardInput = true;
            starter.RedirectStandardOutput = false;
            starter.RedirectStandardError = true;

            try
            {
                var proc = new Process() { StartInfo = starter };
                proc.Start();
                proc.StandardInput.Write(_patchBuilder.ToString());
                proc.StandardInput.Close();

                var err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                var rs = proc.ExitCode == 0;
                proc.Close();

                if (!rs)
                    App.RaiseException(_repo, err);

                return rs;
            }
            catch (Exception e)
            {
                App.RaiseException(_repo, "Failed to unstage changes: " + e.Message);
                return false;
            }
        }

        public async Task<bool> ExecAsync()
        {
            var starter = new ProcessStartInfo();
            starter.WorkingDirectory = _repo;
            starter.FileName = Native.OS.GitExecutable;
            starter.Arguments = "-c core.editor=true update-index --index-info";
            starter.UseShellExecute = false;
            starter.CreateNoWindow = true;
            starter.WindowStyle = ProcessWindowStyle.Hidden;
            starter.RedirectStandardInput = true;
            starter.RedirectStandardOutput = false;
            starter.RedirectStandardError = true;

            try
            {
                var proc = new Process() { StartInfo = starter };
                proc.Start();
                await proc.StandardInput.WriteAsync(_patchBuilder.ToString());
                proc.StandardInput.Close();

                var err = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();
                var rs = proc.ExitCode == 0;
                proc.Close();

                if (!rs)
                    App.RaiseException(_repo, err);

                return rs;
            }
            catch (Exception e)
            {
                App.RaiseException(_repo, "Failed to unstage changes: " + e.Message);
                return false;
            }
        }

        private string _repo = "";
        private StringBuilder _patchBuilder = new StringBuilder();
    }
}
