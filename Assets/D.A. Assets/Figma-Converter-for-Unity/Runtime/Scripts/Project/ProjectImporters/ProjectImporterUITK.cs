using DA_Assets.FCU.Model;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS1998

namespace DA_Assets.FCU
{
    public sealed class ProjectImporterUITK : ProjectImporterBase
    {
        protected override async Task<List<FObject>> ShowLayoutUpdaterWindow(
            SyncHelper[] syncHelpers,
            List<FObject> currentPage,
            CancellationToken token)
        {
            return null;
        }

        protected override Task LoadPrefabs(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override Task DrawGameObjects(FObject virtualPage, CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override async Task FinalSteps(FObject virtualPage, List<FObject> currentPage, CancellationToken token)
        {
            await monoBeh.NameSetter.Set_UITK_Names(currentPage);

#if FCU_EXISTS && FCU_UITK_EXT_EXISTS
            await monoBeh.UITK_Converter.Convert(virtualPage, currentPage, token);
#endif
        }
    }
}