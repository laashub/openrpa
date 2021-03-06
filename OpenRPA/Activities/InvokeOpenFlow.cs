﻿using System;
using System.Activities;
using OpenRPA.Interfaces;
using System.Activities.Presentation.PropertyEditing;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OpenRPA.Activities
{
    [System.ComponentModel.Designer(typeof(InvokeOpenFlowDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.toolbox.invokezeniverseworkflow.png")]
    //[designer.ToolboxTooltip(Text = "Find an Windows UI element based on xpath selector")]
    public class InvokeOpenFlow : NativeActivity
    {
        [RequiredArgument, LocalizedDisplayName("activity_workflow", typeof(Resources.strings)), LocalizedDescription("activity_workflow_help", typeof(Resources.strings))]
        public string workflow { get; set; }
        [RequiredArgument, LocalizedDisplayName("activity_waitforcompleted", typeof(Resources.strings)), LocalizedDescription("activity_ignoreerrors_help", typeof(Resources.strings))]
        public InArgument<bool> WaitForCompleted { get; set; } = true;
        protected override async void Execute(NativeActivityContext context)
        {
            string WorkflowInstanceId = context.WorkflowInstanceId.ToString();
            bool waitforcompleted = WaitForCompleted.Get(context);
            string bookmarkname = null;
            IDictionary<string, object> _payload = new System.Dynamic.ExpandoObject();
            var vars = context.DataContext.GetProperties();
            foreach (dynamic v in vars)
            {
                var value = v.GetValue(context.DataContext);
                if (value != null)
                {
                    //_payload.Add(v.DisplayName, value);
                    try
                    {
                        var test = new { value = value };
                        if (value.GetType() == typeof(System.Data.DataTable)) continue;
                        if (value.GetType() == typeof(System.Data.DataView)) continue;
                        if (value.GetType() == typeof(System.Data.DataRowView)) continue;
                        //
                        var asjson = JObject.FromObject(test);
                        _payload[v.DisplayName] = value;
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    _payload[v.DisplayName] = value;
                }
            }
            try
            {
                bookmarkname = Guid.NewGuid().ToString().Replace("{", "").Replace("}", "").Replace("-", "");
                if(waitforcompleted) context.CreateBookmark(bookmarkname, new BookmarkCallback(OnBookmarkCallback));
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                throw;
            }
            try
            {
                if (!string.IsNullOrEmpty(bookmarkname))
                {
                    var result = await global.webSocketClient.QueueMessage(workflow, _payload, bookmarkname);
                }
            }
            catch (Exception ex)
            {
                var i = WorkflowInstance.Instances.Where(x => x.InstanceId == WorkflowInstanceId).FirstOrDefault();
                if(i != null)
                {
                    i.Abort(ex.Message);
                }
                //context.RemoveBookmark(bookmarkname);
                Log.Error(ex.ToString());
            }
        }
        void OnBookmarkCallback(NativeActivityContext context, Bookmark bookmark, object obj)
        {
            bool waitforcompleted = WaitForCompleted.Get(context);
            if (!waitforcompleted) return;
            // context.RemoveBookmark(bookmark.Name);
            var _msg = JObject.Parse(obj.ToString());
            JObject payload = _msg; // Backward compatible with older version of openflow
            if (_msg.ContainsKey("payload")) payload = _msg.Value<JObject>("payload");
            var state = _msg["state"].ToString();
            if (!string.IsNullOrEmpty(state))
            {
                if (state == "idle")
                {
                    Log.Output("Workflow out node set to idle, so also going idle again.");
                    context.CreateBookmark(bookmark.Name, new BookmarkCallback(OnBookmarkCallback));
                    return;
                }
                else if (state == "failed")
                {
                    var message = "Invoke OpenFlow Workflow failed";
                    if (_msg.ContainsKey("error")) message = _msg["error"].ToString();
                    if (_msg.ContainsKey("_error")) message = _msg["_error"].ToString();
                    if (payload.ContainsKey("error")) message = payload["error"].ToString();
                    if (payload.ContainsKey("_error")) message = payload["_error"].ToString();
                    if (string.IsNullOrEmpty(message)) message = "Invoke OpenFlow Workflow failed";
                    throw new Exception(message);
                }
            }

            List<string> keys = payload.Properties().Select(p => p.Name).ToList();
            foreach (var key in keys)
            {
                var myVar = context.DataContext.GetProperties().Find(key, true);
                if (myVar != null)
                {
                    if(myVar.PropertyType.Name == "JArray")
                    {
                        var json = payload[key].ToString();
                        var jobj = JArray.Parse(json);
                        myVar.SetValue(context.DataContext, jobj);
                    } else if (myVar.PropertyType.Name == "JObject")
                    {
                        var json = payload[key].ToString();
                        var jobj = JObject.Parse(json);
                        myVar.SetValue(context.DataContext, jobj);
                    }
                    else
                    {
                        myVar.SetValue(context.DataContext, payload[key].ToString());
                    }
                    //var myValue = myVar.GetValue(context.DataContext);

                }
                else
                {
                    Log.Debug("Recived property " + key + " but no variable exists to save the value in " + payload[key]);
                }
                //action.setvariable(key, payload[key]);

            }
        }
        protected override bool CanInduceIdle
        {
            get
            {
                return true;
            }
        }
        [LocalizedDisplayName("activity_displayname", typeof(Resources.strings)), LocalizedDescription("activity_displayname_help", typeof(Resources.strings))]
        public new string DisplayName
        {
            get
            {
                return base.DisplayName;
            }
            set
            {
                base.DisplayName = value;
            }
        }
    }
}