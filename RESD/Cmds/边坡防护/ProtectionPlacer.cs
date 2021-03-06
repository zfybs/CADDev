﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using eZcad;
using eZcad_AddinManager;
using RESD.Cmds;
using RESD.ParameterForm;
using RESD.Utility;
using RESD.AppSetup;

[assembly: CommandClass(typeof(ProtectionPlacer))]

namespace RESD.Cmds
{
    [EcDescription(CommandDescription)]
    public class ProtectionPlacer : ICADExCommand
    {
        private DocumentModifier _docMdf;

        #region --- 命令设计

        /// <summary> 命令行命令名称，同时亦作为命令语句所对应的C#代码中的函数的名称 </summary>
        public const string CommandName = "PlaceProtection";
        private const string CommandText = @"防护布置";
        private const string CommandDescription = @"放置边坡防护的文字";

        /// <summary> 放置边坡防护的文字 </summary>
        [CommandMethod(AddinOptions.GroupCommnad, CommandName, CommandFlags.UsePickSet)
        , DisplayName(CommandText), Description(CommandDescription)
            , RibbonItem(CommandText, CommandDescription, AddinOptions.ImageDirectory + "PlaceProtection_32.png")]
        public void PlaceProtection()
        {
            DocumentModifier.ExecuteCommand(PlaceProtection);
        }

        public ExternalCommandResult Execute(SelectionSet impliedSelection, ref string errorMessage,
            ref IList<ObjectId> elementSet)
        {
            var s = new ProtectionPlacer();
            return AddinManagerDebuger.DebugInAddinManager(s.PlaceProtection,
                impliedSelection, ref errorMessage, ref elementSet);
        }

        #endregion

        /// <summary> 放置边坡防护的文字 </summary>
        public ExternalCmdResult PlaceProtection(DocumentModifier docMdf, SelectionSet impliedSelection)
        {
            _docMdf = docMdf;
            var fm = PF_PlaceProt.GetUniqueInstance(docMdf, impliedSelection);
            var res = fm.ShowDialog();
            if (res == DialogResult.OK)
            {
                return ExternalCmdResult.Commit;
            }
            else
            {
                return ExternalCmdResult.Cancel;
            }
        }

        #region --- 通过命令行的方式进行操作

        /// <summary> 放置边坡防护的文字 </summary>
        public ExternalCmdResult PlaceProtectionOnCommandLine(DocumentModifier docMdf, SelectionSet impliedSelection)
        {
            bool changeSingle;
            string newText;
            var succ = SetNewText(docMdf, out newText, out changeSingle);
            if (succ)
            {
                //
                if (changeSingle)
                {
                    ChangeSingleText(newText);
                }
                else
                {
                    ChangeMultiTexts(newText);
                }
            }
            return ExternalCmdResult.Commit;
        }

        #region --- 基本操作选项的设置

        /// <summary> 从两个选项中选择一个 </summary>
        /// <param name="docMdf"></param>
        /// <returns>true 表示按顶点缩放（默认值），false 表示按长度缩放</returns>
        private static bool SetNewText(DocumentModifier docMdf, out string newText, out bool changeSingle)
        {
            changeSingle = true;
            newText = "";
            var op = new PromptStringOptions(message: "\n设置要放置的防护方式字符[多个(M)]:"); // 默认值写在前面
            op.AllowSpaces = false;

            var res = docMdf.acEditor.GetString(op);

            if (res.Status == PromptStatus.OK)
            {
                if (res.StringResult.Equals("m", StringComparison.CurrentCultureIgnoreCase))
                {
                    changeSingle = false;
                    newText = SetNewText(docMdf);
                }
                else
                {
                    changeSingle = true;
                    newText = res.StringResult;
                }
                if (newText == null)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        private static string SetNewText(DocumentModifier docMdf)
        {
            var op = new PromptStringOptions(message: "\n设置要放置的防护方式字符:"); // 默认值写在前面
            op.AllowSpaces = true;

            var res = docMdf.acEditor.GetString(op);

            if (res.Status == PromptStatus.OK)
            {
                return res.StringResult;
            }
            return null;
        }

        #endregion

        #region --- 一个一个修改

        private void ChangeSingleText(string newText)
        {
            bool cont;
            var txt = GetDbTextEntity(_docMdf.acEditor, out cont);

            while (cont)
            {
                if (txt != null)
                {
                    ChangeText(txt, newText);
                }
                txt = GetDbTextEntity(_docMdf.acEditor, out cont);
            }
        }

        private DBText GetDbTextEntity(Editor ed, out bool cont)
        {
            var op = new PromptEntityOptions("\n选择要修改的单行文字");
            op.SetRejectMessage($"\n选择的单行文字必须位于图层“{SQConstants.LayerName_ProtectionMethod_Slope}”或“{SQConstants.LayerName_ProtectionMethod_Platform}”中");
            op.AddAllowedClass(typeof(DBText), exactMatch: true);
            var res = ed.GetEntity(op);

            cont = true;
            if (res.Status == PromptStatus.OK)
            {
                var pl = res.ObjectId.GetObject(OpenMode.ForRead) as DBText;
                if (pl != null)
                {
                    if ((pl.Layer == SQConstants.LayerName_ProtectionMethod_Slope)
                        || (pl.Layer == SQConstants.LayerName_ProtectionMethod_Platform))
                    {
                        cont = true;
                        return pl;
                    }
                }
            }
            else if (res.Status == PromptStatus.Cancel)
            {
                cont = false;
            }

            return null;
        }

        #endregion

        #region --- 一次修改多个

        private void ChangeMultiTexts(string newText)
        {
            var txts = SelectDbtexts(_docMdf.acEditor);
            if (txts != null && txts.Length > 0)
            {
                foreach (var txt in txts)
                {
                    ChangeText(txt, newText);
                }
            }
        }


        private DBText[] SelectDbtexts(Editor ed)
        {
            // 创建一个 TypedValue 数组，用于定义过滤条件
            var acTypValAr = new TypedValue[]
            {
                  new TypedValue((int) DxfCode.Operator, "<OR"),
                new TypedValue((int) DxfCode.LayerName, SQConstants.LayerName_ProtectionMethod_Slope),
                new TypedValue((int) DxfCode.LayerName, SQConstants.LayerName_ProtectionMethod_Platform),
                new TypedValue((int) DxfCode.Operator, "OR>")
            };

            var pso = new PromptSelectionOptions();

            // pso.Keywords.Add("NoFilter", "无(N)", "无(N)"); //

            // Set our prompts to include our keywords
            var kws = pso.Keywords.GetDisplayString(true);
            pso.MessageForAdding = $"\n选择多个要修改的单行文字 " + kws; // 当用户在命令行中输入A（或Add）时，命令行出现的提示字符。
            pso.MessageForRemoval = pso.MessageForAdding; // 当用户在命令行中输入Re（或Remove）时，命令行出现的提示字符。

            // 请求在图形区域选择对象
            var res = ed.GetSelection(pso, new SelectionFilter(acTypValAr));

            // 如果提示状态OK，表示对象已选
            if (res.Status == PromptStatus.OK)
            {
                var ids = res.Value.GetObjectIds();
                var txts = ids.Select(r => r.GetObject(OpenMode.ForRead) as DBText).ToArray();
                return txts;
            }
            else
            {
                return null;
            }
        }

        #endregion

        private void ChangeText(DBText txt, string newText)
        {
            if (!string.IsNullOrEmpty(newText))
            {
                txt.UpgradeOpen();
                txt.TextString = newText;
                txt.Draw();
                txt.DowngradeOpen();
            }
        }
        #endregion
    }
}