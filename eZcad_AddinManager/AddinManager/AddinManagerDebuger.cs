﻿using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using eZcad;
using eZcad.Utility;

namespace eZcad_AddinManager
{
    /// <summary>
    /// 用来辅助调试工作
    /// </summary>
    internal class AddinManagerDebuger
    {
        internal static ExternalCommandResult DebugInAddinManager(ExternalCommand cmd,
            SelectionSet impliedSelection, ref string errorMessage, ref IList<ObjectId> elementSet)
        {
            //var dat = new DllActivator_eZcad();
            //dat.ActivateReferences();

            using (var docMdf = new DocumentModifier(true))
            {
                try
                {
                    // 先换个行，显示效果更清爽
                    docMdf.WriteNow("\n");

                    var canCommit = cmd(docMdf, impliedSelection);
                    //
                    switch (canCommit)
                    {
                        case ExternalCmdResult.Commit:
                            docMdf.acTransaction.Commit();
                            return ExternalCommandResult.Succeeded;
                            break;
                        case ExternalCmdResult.Cancel:
                            docMdf.acTransaction.Abort();
                            return ExternalCommandResult.Cancelled;
                            break;
                        default:
                            docMdf.acTransaction.Abort();
                            return ExternalCommandResult.Cancelled;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    docMdf.acTransaction.Abort(); // Abort the transaction and rollback to the previous state
                    errorMessage = ex.Message + "\r\n\r\n" + ex.StackTrace;
                    return ExternalCommandResult.Failed;
                }
            }
        }

        /// <summary>
        /// 具体的高度操作的代码模板
        /// </summary>
        /// <param name="docMdf"></param>
        /// <param name="impliedSelection"></param>
        private void DoSomethingTemplate(DocumentModifier docMdf, SelectionSet impliedSelection)
        {
            var obj = SelectUtils.PickEntity<Entity>(docMdf.acEditor);
            if (obj != null)
            {
                var blkTb =
                    docMdf.acTransaction.GetObject(docMdf.acDataBase.BlockTableId, OpenMode.ForRead) as BlockTable;
                var btr =
                    docMdf.acTransaction.GetObject(blkTb[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as
                        BlockTableRecord;

                var ent = new DBText();

                // 将新对象添加到块表记录和事务
                btr.AppendEntity(ent);
                docMdf.acTransaction.AddNewlyCreatedDBObject(ent, true);
            }
        }
    }
}