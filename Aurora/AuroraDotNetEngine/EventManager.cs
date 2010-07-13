/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules;
using OpenSim.Region;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using Aurora.ScriptEngine.AuroraDotNetEngine.APIs.Interfaces;
using Aurora.ScriptEngine.AuroraDotNetEngine.Plugins;
using Aurora.ScriptEngine.AuroraDotNetEngine.Runtime;
using log4net;

namespace Aurora.ScriptEngine.AuroraDotNetEngine
{
    /// <summary>
    /// Prepares events so they can be directly executed upon a script by EventQueueManager, then queues it.
    /// </summary>
    [Serializable]
    public class EventManager
    {
        //
        // Class is instanced in "ScriptEngine" and Uses "EventQueueManager"
        // that is also instanced in "ScriptEngine".
        // This class needs a bit of explaining:
        //
        // This class it the link between an event inside OpenSim and
        // the corresponding event in a user script being executed.
        //
        // For example when an user touches an object then the
        // "scene.EventManager.OnObjectGrab" event is fired
        // inside OpenSim.
        // We hook up to this event and queue a touch_start in
        // EventQueueManager with the proper LSL parameters.
        // It will then be delivered to the script by EventQueueManager.
        //
        // You can check debug C# dump of an LSL script if you need to
        // verify what exact parameters are needed.
        //

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ScriptEngine m_scriptEngine;

        public EventManager(ScriptEngine _ScriptEngine, bool performHookUp)
        {
            m_scriptEngine = _ScriptEngine;
        }

        public void HookUpRegionEvents(Scene scene)
        {
            //m_log.Info("[" + myScriptEngine.ScriptEngineName +
            //           "]: Hooking up to server events");

            scene.EventManager.OnObjectGrab +=
                    touch_start;
            scene.EventManager.OnObjectGrabbing += 
                    touch;
            scene.EventManager.OnObjectDeGrab +=
                    touch_end;
            scene.EventManager.OnRemoveScript +=
                    OnRemoveScript;
            scene.EventManager.OnScriptChangedEvent +=
                    changed;
            scene.EventManager.OnScriptAtTargetEvent +=
                    at_target;
            scene.EventManager.OnScriptNotAtTargetEvent +=
                    not_at_target;
            scene.EventManager.OnScriptAtRotTargetEvent +=
                    at_rot_target;
            scene.EventManager.OnScriptNotAtRotTargetEvent +=
                    not_at_rot_target;
            scene.EventManager.OnScriptControlEvent +=
                    control;
            scene.EventManager.OnScriptColliderStart +=
                    collision_start;
            scene.EventManager.OnScriptColliding +=
                    collision;
            scene.EventManager.OnScriptCollidingEnd +=
                    collision_end;
            scene.EventManager.OnScriptLandColliderStart += 
                    land_collision_start;
            scene.EventManager.OnScriptLandColliding += 
                    land_collision;
            scene.EventManager.OnScriptLandColliderEnd += 
                    land_collision_end;
            scene.EventManager.OnAttach += attach;


            IMoneyModule money =
                    scene.RequestModuleInterface<IMoneyModule>();
            if (money != null)
                money.OnObjectPaid+=HandleObjectPaid;
        }

        private void HandleObjectPaid(UUID objectID, UUID agentID, int amount)
        {
            SceneObjectPart part =
                    m_scriptEngine.findPrimsScene(objectID).GetSceneObjectPart(objectID);

            if (part == null)
                return;

            m_log.Debug("Paid: " + objectID + " from " + agentID + ", amount " + amount);
            if (part.ParentGroup != null)
                part = part.ParentGroup.RootPart;

            if (part != null)
            {
                money(part.LocalId, agentID, amount);
            }
        }

        public void changed(uint localID, uint change)
        {
            // Add to queue for all scripts in localID, Object pass change.
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "changed",new object[] { new LSL_Types.LSLInteger(change) },
                    new DetectParams[0]));
        }

        public void state_entry(uint localID)
        {
            // Add to queue for all scripts in ObjectID object
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "state_entry",new object[] { },
                    new DetectParams[0]));
        }

        /// <summary>
        /// Handles piping the proper stuff to The script engine for touching
        /// Including DetectedParams
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="originalID"></param>
        /// <param name="offsetPos"></param>
        /// <param name="remoteClient"></param>
        /// <param name="surfaceArgs"></param>
        public void touch_start(uint localID, uint originalID, Vector3 offsetPos,
                IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
            // Add to queue for all scripts in ObjectID object
            DetectParams[] det = new DetectParams[1];
            det[0] = new DetectParams();
            det[0].Key = remoteClient.AgentId;

            if (originalID == 0)
            {
                det[0].Populate(m_scriptEngine.findPrimsScene(localID));
                SceneObjectPart part = m_scriptEngine.findPrimsScene(localID).GetSceneObjectPart(localID);
                if (part == null)
                    return;

                det[0].LinkNum = part.LinkNum;
            }
            else
            {
                det[0].Populate(m_scriptEngine.findPrimsScene(originalID));
                SceneObjectPart originalPart = m_scriptEngine.findPrimsScene(originalID).GetSceneObjectPart(originalID);
                det[0].LinkNum = originalPart.LinkNum;
            }

            if (surfaceArgs != null)
            {
                det[0].SurfaceTouchArgs = surfaceArgs;
            }

            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "touch_start", new Object[] { new LSL_Types.LSLInteger(1) },
                    det));
        }

        public void touch(uint localID, uint originalID, Vector3 offsetPos,
                IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
            // Add to queue for all scripts in ObjectID object
            DetectParams[] det = new DetectParams[1];
            det[0] = new DetectParams();
            det[0].Key = remoteClient.AgentId;
            det[0].OffsetPos = new LSL_Types.Vector3(offsetPos.X,
                                                     offsetPos.Y,
                                                     offsetPos.Z);

            if (originalID == 0)
            {
                det[0].Populate(m_scriptEngine.findPrimsScene(localID));
                SceneObjectPart part = m_scriptEngine.findPrimsScene(localID).GetSceneObjectPart(localID);
                if (part == null)
                    return;

                det[0].LinkNum = part.LinkNum;
            }
            else
            {
                det[0].Populate(m_scriptEngine.findPrimsScene(originalID));
                SceneObjectPart originalPart = m_scriptEngine.findPrimsScene(originalID).GetSceneObjectPart(originalID);
                det[0].LinkNum = originalPart.LinkNum;
            }
            if (surfaceArgs != null)
            {
                det[0].SurfaceTouchArgs = surfaceArgs;
            }

            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "touch", new Object[] { new LSL_Types.LSLInteger(1) },
                    det));
        }

        public void touch_end(uint localID, uint originalID, IClientAPI remoteClient,
                              SurfaceTouchEventArgs surfaceArgs)
        {
            // Add to queue for all scripts in ObjectID object
            DetectParams[] det = new DetectParams[1];
            det[0] = new DetectParams();
            det[0].Key = remoteClient.AgentId;

            if (originalID == 0)
            {
                det[0].Populate(m_scriptEngine.findPrimsScene(localID));
                SceneObjectPart part = m_scriptEngine.findPrimsScene(localID).GetSceneObjectPart(localID);
                if (part == null)
                    return;

                det[0].LinkNum = part.LinkNum;
            }
            else
            {
                det[0].Populate(m_scriptEngine.findPrimsScene(originalID));
                SceneObjectPart originalPart = m_scriptEngine.findPrimsScene(originalID).GetSceneObjectPart(originalID);
                det[0].LinkNum = originalPart.LinkNum;
            }

            if (surfaceArgs != null)
            {
                det[0].SurfaceTouchArgs = surfaceArgs;
            }

            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "touch_end", new Object[] { new LSL_Types.LSLInteger(1) },
                    det));
        }

        public void OnRemoveScript(uint localID, UUID itemID)
        {
            m_scriptEngine.StopScript(
                localID,
                itemID);
        }

        public void money(uint localID, UUID agentID, int amount)
        {
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "money", new object[] {
                    new LSL_Types.LSLString(agentID.ToString()),
                    new LSL_Types.LSLInteger(amount) },
                    new DetectParams[0]));
        }

        public void state_exit(uint localID)
        {
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "state_exit", new object[0] { },
                    new DetectParams[0]));
        }

        public void collision_start(uint localID, ColliderArgs col)
        {
            // Add to queue for all scripts in ObjectID object
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Key = detobj.keyUUID;
                d.Populate(m_scriptEngine.findPrimsScene(localID));
                det.Add(d);
            }

            if (det.Count > 0)
                m_scriptEngine.PostObjectEvent(localID, new EventParams(
                        "collision_start",
                        new Object[] { new LSL_Types.LSLInteger(det.Count) },
                        det.ToArray()));
        }

        public void collision(uint localID, ColliderArgs col)
        {
            // Add to queue for all scripts in ObjectID object
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Key = detobj.keyUUID;
                d.Populate(m_scriptEngine.findPrimsScene(localID));
                det.Add(d);
            }

            if (det.Count > 0)
                m_scriptEngine.PostObjectEvent(localID, new EventParams(
                        "collision", new Object[] { new LSL_Types.LSLInteger(det.Count) },
                        det.ToArray()));
        }

        public void collision_end(uint localID, ColliderArgs col)
        {
            // Add to queue for all scripts in ObjectID object
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Key = detobj.keyUUID;
                d.Populate(m_scriptEngine.findPrimsScene(localID));
                det.Add(d);
            }

            if (det.Count > 0)
                m_scriptEngine.PostObjectEvent(localID, new EventParams(
                        "collision_end",
                        new Object[] { new LSL_Types.LSLInteger(det.Count) },
                        det.ToArray()));
        }

        public void land_collision_start(uint localID, ColliderArgs col)
        {
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Position = new LSL_Types.Vector3(detobj.posVector.X,
                    detobj.posVector.Y,
                    detobj.posVector.Z);
                d.Populate(m_scriptEngine.findPrimsScene(localID));
                det.Add(d);
                m_scriptEngine.PostObjectEvent(localID, new EventParams(
                        "land_collision_start",
                        new Object[] { new LSL_Types.Vector3(d.Position) },
                        det.ToArray()));
            }

        }

        public void land_collision(uint localID, ColliderArgs col)
        {
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Position = new LSL_Types.Vector3(detobj.posVector.X,
                    detobj.posVector.Y,
                    detobj.posVector.Z);
                d.Populate(m_scriptEngine.findPrimsScene(localID));
                det.Add(d);
                m_scriptEngine.PostObjectEvent(localID, new EventParams(
                        "land_collision",
                        new Object[] { new LSL_Types.Vector3(d.Position) },
                        det.ToArray()));
            }
        }

        public void land_collision_end(uint localID, ColliderArgs col)
        {
            List<DetectParams> det = new List<DetectParams>();

            foreach (DetectedObject detobj in col.Colliders)
            {
                DetectParams d = new DetectParams();
                d.Position = new LSL_Types.Vector3(detobj.posVector.X,
                    detobj.posVector.Y,
                    detobj.posVector.Z);
                d.Populate(m_scriptEngine.findPrimsScene(localID));
                det.Add(d);
                m_scriptEngine.PostObjectEvent(localID, new EventParams(
                        "land_collision_end",
                        new Object[] { new LSL_Types.Vector3(d.Position) },
                        det.ToArray()));
            }
        }

        public void control(uint localID, UUID itemID, UUID agentID, uint held, uint change)
        {
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "control",new object[] {
                    new LSL_Types.LSLString(agentID.ToString()),
                    new LSL_Types.LSLInteger(held),
                    new LSL_Types.LSLInteger(change)},
                    new DetectParams[0]));
        }

        public void email(uint localID, UUID itemID, string timeSent,
                string address, string subject, string message, int numLeft)
        {
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "email", new object[] {
                    new LSL_Types.LSLString(timeSent),
                    new LSL_Types.LSLString(address),
                    new LSL_Types.LSLString(subject),
                    new LSL_Types.LSLString(message),
                    new LSL_Types.LSLInteger(numLeft)},
                    new DetectParams[0]));
        }

        public void at_target(uint localID, uint handle, Vector3 targetpos,
                Vector3 atpos)
        {
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "at_target", new object[] {
                    new LSL_Types.LSLInteger(handle),
                    new LSL_Types.Vector3(targetpos.X,targetpos.Y,targetpos.Z),
                    new LSL_Types.Vector3(atpos.X,atpos.Y,atpos.Z) },
                    new DetectParams[0]));
        }

        public void not_at_target(uint localID)
        {
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "not_at_target", new object[0],
                    new DetectParams[0]));
        }

        public void at_rot_target(uint localID, uint handle, Quaternion targetrot,
                Quaternion atrot)
        {
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "at_rot_target", new object[] {
                    new LSL_Types.LSLInteger(handle),
                    new LSL_Types.Quaternion(targetrot.X,targetrot.Y,targetrot.Z,targetrot.W),
                    new LSL_Types.Quaternion(atrot.X,atrot.Y,atrot.Z,atrot.W) },
                    new DetectParams[0]));
        }

        public void not_at_rot_target(uint localID)
        {
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "not_at_rot_target", new object[0],
                    new DetectParams[0]));
        }

        public void attach(uint localID, UUID itemID, UUID avatar)
        {
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "attach", new object[] {
                    new LSL_Types.LSLString(avatar.ToString()) },
                    new DetectParams[0]));
        }

        public void moving_start(uint localID, UUID itemID)
        {
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "moving_start", new object[0],
                    new DetectParams[0]));
        }

        public void moving_end(uint localID, UUID itemID)
        {
            m_scriptEngine.PostObjectEvent(localID, new EventParams(
                    "moving_end", new object[0],
                    new DetectParams[0]));
        }

        /// <summary>
        /// If set to true then threads and stuff should try to make a graceful exit
        /// </summary>
        public bool PleaseShutdown
        {
            get { return _PleaseShutdown; }
            set { _PleaseShutdown = value; }
        }
        private bool _PleaseShutdown = false;
    }
}
