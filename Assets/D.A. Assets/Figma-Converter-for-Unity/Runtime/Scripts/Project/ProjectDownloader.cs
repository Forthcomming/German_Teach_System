using DA_Assets.Constants;
using DA_Assets.DAI;
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.FCU.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DA_Assets.FCU
{
    [Serializable]
    public class ProjectDownloader : FcuBase
    {
        public async Task<ImportResult> DownloadProject()
        {
            ImportResult result;

            monoBeh.AssetTools.StopAsset(ImportStatus.Technical);
            monoBeh.ProjectImporter.ImportTokenSource = new CancellationTokenSource();

            try
            {
                await DownloadProjectAsync(monoBeh.ProjectImporter.ImportTokenSource.Token);
                monoBeh.Events.OnProjectDownloaded?.Invoke(monoBeh);
                result = monoBeh.AssetTools.StopAsset(ImportStatus.ProjectDownloadSuccess);
            }
            catch (OperationCanceledException)
            {
                result = monoBeh.AssetTools.StopAsset(ImportStatus.Stopped);
            }
            catch (Exception ex)
            {
                result = monoBeh.AssetTools.StopAsset(ImportStatus.Exception, ex);
                monoBeh.Events.OnProjectDownloadFail?.Invoke(monoBeh);
            }
            finally
            {
                monoBeh.EditorDelegateHolder.CompleteProgress?.Invoke(monoBeh, ProgressBarCategory.ProjectDownloading);
            }

            return result;
        }

        private async Task DownloadProjectAsync(CancellationToken token, CancellationTokenSource timeoutCts = null)
        {
            if (monoBeh.IsJsonNetExists() == false)
            {
                throw new Exception(FcuLocKey.log_cant_find_package.Localize(DAConstants.JsonNetPackageName));
            }

            if (!monoBeh.Authorizer.IsAuthed())
            {
                throw new Exception("Not authorized. Please authenticate first.");
            }

            if (string.IsNullOrWhiteSpace(monoBeh.Settings.MainSettings.ProjectId))
            {
                throw new Exception(FcuLocKey.log_incorrent_project_url.Localize());
            }

            monoBeh.Events.OnProjectDownloadStart?.Invoke(monoBeh);
            monoBeh.InspectorDrawer.SelectableDocument.Childs.Clear();
            monoBeh.EditorDelegateHolder.StartProgress?.Invoke(monoBeh, ProgressBarCategory.ProjectDownloading, 0, true);

            DARequest projectRequest = RequestCreator.CreateProjectRequest(
                monoBeh.RequestSender.GetRequestHeader(monoBeh.Authorizer.Token),
                monoBeh.Settings.MainSettings.ProjectId,
                FcuConfig.FrameListDepth);

            DAResult<FigmaProject> result = await monoBeh.RequestSender.SendRequest<FigmaProject>(projectRequest, token);

            if (!result.Success)
            {
                string message;

                switch (result.Error.status)
                {
                    case 403:
                        message = FcuLocKey.log_need_auth.Localize();
                        break;
                    case 404:
                        message = FcuLocKey.log_project_not_found.Localize();
                        break;
                    default:
                        message = FcuLocKey.log_unknown_error.Localize(result.Error.err, result.Error.status, result.Error.exception);
                        break;
                }

                throw new Exception(message);
            }

            monoBeh.CurrentProject.FigmaProject = result.Object;
            monoBeh.CurrentProject.ProjectName = result.Object.Name;
            monoBeh.InspectorDrawer.FillSelectableFramesArray(monoBeh.CurrentProject.FigmaProject.Document);
        }

        public async Task<FigmaProject> GetFileStructureAsync(int depth, CancellationToken ct = default)
        {
            DARequest projectRequest = RequestCreator.CreateFileStructRequest(
                    monoBeh.RequestSender.GetRequestHeader(monoBeh.Authorizer.Token),
                    monoBeh.Settings.MainSettings.ProjectId,
                    depth);

            DAResult<FigmaProject> result = await monoBeh.RequestSender.SendRequest<FigmaProject>(projectRequest, ct);
            return result.Object;
        }

        public async Task<List<FObject>> DownloadAllNodes(string[] selectedIds, CancellationToken token)
        {
            ConcurrentBag<List<FObject>> nodeChunks = new ConcurrentBag<List<FObject>>();
            List<List<string>> idChunks = selectedIds.Split(FcuConfig.ChunkSizeGetNodes);
            List<Task> downloadTasks = new List<Task>(idChunks.Count);
            int completedChunks = 0;
            int tempCount = -1;

            monoBeh.EditorDelegateHolder.StartProgress?.Invoke(monoBeh, ProgressBarCategory.DownloadingNodes, idChunks.Count, false);

            try
            {
                foreach (List<string> chunk in idChunks)
                {
                    if (token.IsCancellationRequested)
                        break;

                    string ids = string.Join(",", chunk);
                    DARequest projectRequest = RequestCreator.CreateNodeRequest(
                        monoBeh.RequestSender.GetRequestHeader(monoBeh.Authorizer.Token),
                        monoBeh.Settings.MainSettings.ProjectId,
                        ids);

                    Task downloadTask = Task.Run(async () =>
                    {
                        try
                        {
                            DAResult<FigmaProject> result = await monoBeh.RequestSender.SendRequest<FigmaProject>(projectRequest, token);

                            if (result.Success)
                            {
                                List<FObject> docs = new List<FObject>();

                                if (!result.Object.IsDefault() && !result.Object.Nodes.IsEmpty())
                                {
                                    foreach (var item in result.Object.Nodes)
                                    {
                                        if (item.Value.IsDefault())
                                            continue;

                                        docs.Add(item.Value.Document);
                                    }
                                }

                                nodeChunks.Add(docs);
                            }
                            else
                            {
                                nodeChunks.Add(default);
                                Debug.LogError(FcuLocKey.log_cant_get_part_of_frames.Localize(result.Error.err, result.Error.status));
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            nodeChunks.Add(default);
                            Debug.LogException(ex);
                        }
                        finally
                        {
                            int current = Interlocked.Increment(ref completedChunks);
                            monoBeh.EditorDelegateHolder.UpdateProgress?.Invoke(monoBeh, ProgressBarCategory.DownloadingNodes, current);
                            FcuLogger.WriteLogBeforeEqual(nodeChunks, idChunks, FcuLocKey.log_getting_frames, nodeChunks.CountAll(), selectedIds.Count(), ref tempCount);
                        }
                    }, token);

                    downloadTasks.Add(downloadTask);
                }

                await Task.WhenAll(downloadTasks);
            }
            finally
            {
                monoBeh.EditorDelegateHolder.CompleteProgress?.Invoke(monoBeh, ProgressBarCategory.DownloadingNodes);
            }

            return nodeChunks.FromChunks();
        }
    }
}