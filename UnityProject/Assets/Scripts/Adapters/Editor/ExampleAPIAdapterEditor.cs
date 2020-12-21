//该功能由JEngine作者掉尽头发而得，自动生成跨域适配器的编辑器，所以你还有什么理由不使用JEngine~
#if UNITY_EDITOR
using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using JEngine.Core;
using LitJson;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;


[CustomEditor(typeof(ExampleAPI), true)]
public class ExampleAPIAdapterEditor : Editor
{
    private bool displaying;
        
    private AnimBool[] fadeGroup = new AnimBool[2];
    private async void OnEnable()
    {
        for(int i=0;i<fadeGroup.Length;i++)
        {
            fadeGroup[i] = new AnimBool(false);
            this.fadeGroup[i].valueChanged.AddListener(this.Repaint);
        }
        
        displaying = true;
        
        while (true && Application.isEditor && Application.isPlaying && displaying)
        {
            try
            {
                this.Repaint();
            }
            catch{}
            await Task.Delay(500);
        }
    }
    
    private void OnDestroy()
    {
        displaying = false;
    }
    
    private void OnDisable()
    {
        // 移除动画监听
        for(int i=0;i<fadeGroup.Length;i++)
        {
            this.fadeGroup[i].valueChanged.RemoveListener(this.Repaint);
        }
    
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ProjectAdapter.ExampleAPIAdapter.Adapter clr = target as ProjectAdapter.ExampleAPIAdapter.Adapter;
        var instance = clr.ILInstance;
        if (instance != null)
        {
            EditorGUILayout.LabelField("Script", clr.ILInstance.Type.Name);
            
            int index = 0;
            foreach (var i in instance.Type.FieldMapping)
            {
                //这里是取的所有字段，没有处理不是public的
                var name = i.Key;
                var type = instance.Type.FieldTypes[index];
                index++;
    
                var cType = type.TypeForCLR;
                object obj = instance[i.Value];
                if (cType.IsPrimitive) //如果是基础类型
                {
                    GUI.enabled = true;
                    try
                    {
                        if (cType == typeof(float))
                        {
                            instance[i.Value] = EditorGUILayout.FloatField(name, (float) instance[i.Value]);
                        }
                        else if (cType == typeof(double))
                        {
                            instance[i.Value] = EditorGUILayout.DoubleField(name, (float) instance[i.Value]);
                        }
                        else if (cType == typeof(int))
                        {
                            instance[i.Value] = EditorGUILayout.IntField(name, (int) instance[i.Value]);
                        }
                        else if (cType == typeof(long))
                        {
                            instance[i.Value] = EditorGUILayout.LongField(name, (long) instance[i.Value]);
                        }
                        else if (cType == typeof(bool))
                        {
                            var result = bool.TryParse(instance[i.Value].ToString(), out var value);
                            if (!result)
                            {
                                value = instance[i.Value].ToString() == "1";
                            }
                            instance[i.Value] = EditorGUILayout.Toggle(name, value );
                        }
                        else
                        {
                            EditorGUILayout.LabelField(name, instance[i.Value].ToString());
                        }
                    }
                    catch (Exception e)
                    {
                        Log.PrintError($"无法序列化{name}，{e.Message}");
                        EditorGUILayout.LabelField(name, instance[i.Value].ToString());
                    }
                }
                else
                {
                    if (cType == typeof(string))
                    {
                        if (obj != null)
                        {
                            instance[i.Value] = EditorGUILayout.TextField(name, (string) instance[i.Value]);
                        }
                        else
                        {
                            instance[i.Value] = EditorGUILayout.TextField(name, "");
                        }
                    }
                    else if (cType == typeof(JsonData))//可以折叠显示Json数据
                    {
                        if (instance[i.Value] != null)
                        {
                            this.fadeGroup[1].target = EditorGUILayout.Foldout(this.fadeGroup[1].target, name, true);
                            if (EditorGUILayout.BeginFadeGroup(this.fadeGroup[1].faded))
                            {
                                instance[i.Value] = EditorGUILayout.TextArea(
                                    ((JsonData) instance[i.Value]).ToString()
                                );
                            }
                            EditorGUILayout.EndFadeGroup();
                            EditorGUILayout.Space();
                        }
                        else
                        {
                            EditorGUILayout.LabelField(name, "暂无值的JsonData");
                        }
                    }
                    else if (typeof(UnityEngine.Object).IsAssignableFrom(cType))
                    {
                        if (obj == null && cType == typeof(MonoBehaviourAdapter.Adaptor))
                        {
                            EditorGUILayout.LabelField(name, "未赋值的热更类");
                            break;
                        }
    
                        if (cType == typeof(MonoBehaviourAdapter.Adaptor))
                        {
                            try
                            {
                                var clrInstance = ClassBindMgr.FindObjectsOfTypeAll<MonoBehaviourAdapter.Adaptor>()
                                    .Find(adaptor =>
                                        adaptor.ILInstance == instance[i.Value]);
                                GUI.enabled = true;
                                EditorGUILayout.ObjectField(name,clrInstance.gameObject ,typeof(GameObject),true);
                                GUI.enabled = false;
                            }
                            catch
                            {
                                EditorGUILayout.LabelField(name, "未赋值的热更类");
                            }
                            
                            break;
                        }
                        
                        //处理Unity类型
                        var res = EditorGUILayout.ObjectField(name, obj as UnityEngine.Object, cType, true);
                        instance[i.Value] = res;
                    }
                    //可绑定值，可以尝试更改
                    else if (type.ReflectionType.ToString().Contains("BindableProperty") && obj != null)
                    {
                        PropertyInfo fi = type.ReflectionType.GetProperty("Value");
                        object val = fi.GetValue(obj);
    
                        string genericTypeStr = type.ReflectionType.ToString().Split('`')[1].Replace("1<", "")
                            .Replace(">", "");
                        Type genericType = Type.GetType(genericTypeStr);
                        if (genericType == null  || (!genericType.IsPrimitive && genericType != typeof(string)))//不是基础类型或字符串
                        {
                            EditorGUILayout.LabelField(name, val.ToString());//只显示字符串
                        }
                        else
                        {
                            //可更改
                            var data = ConvertSimpleType(EditorGUILayout.TextField(name, val.ToString()), genericType);
                            if (data != null)//尝试更改
                            {
                                fi.SetValue(obj, data);
                            }
                        }
                    }
                    else
                    {
                        //其他类型现在没法处理
                        if (obj != null)
                        {
                            var clrInstance = ClassBindMgr.FindObjectsOfTypeAll<MonoBehaviourAdapter.Adaptor>()
                                .Find(adaptor =>
                                    adaptor.ILInstance.Equals(instance[i.Value]));
                            if (clrInstance != null)
                            {
                                GUI.enabled = true;
                                EditorGUILayout.ObjectField(name,clrInstance.gameObject ,typeof(GameObject),true);
                                GUI.enabled = false;
                            }
                            else
                            {
                                EditorGUILayout.LabelField(name, obj.ToString());
                            }
                        }
                        else
                            EditorGUILayout.LabelField(name, "(null)");
                    }
                }
            }
        }
    
        // 应用属性修改
        this.serializedObject.ApplyModifiedProperties();
    }
    
    
    
    private object ConvertSimpleType(object value, Type destinationType) 
    { 
        object returnValue; 
        if ((value == null) || destinationType.IsInstanceOfType(value)) 
        { 
            return value; 
        } 
        string str = value as string; 
        if ((str != null) && (str.Length == 0)) 
        { 
            return destinationType.IsValueType ? Activator.CreateInstance(destinationType) : null;
        } 
        TypeConverter converter = TypeDescriptor.GetConverter(destinationType); 
        bool flag = converter.CanConvertFrom(value.GetType()); 
        if (!flag) 
        { 
            converter = TypeDescriptor.GetConverter(value.GetType()); 
        } 
        if (!flag && !converter.CanConvertTo(destinationType)) 
        { 
            Log.PrintError("无法转换成类型：'" + value.ToString() + "' ==> " + destinationType); 
        } 
        try 
        { 
            returnValue = flag ? converter.ConvertFrom(null, null, value) : converter.ConvertTo(null, null, value, destinationType); 
        } 
        catch (Exception e)
        {
            Log.PrintError("类型转换出错：'" + value.ToString() + "' ==> " + destinationType + "\n" + e.Message);
            returnValue = destinationType.IsValueType ? Activator.CreateInstance(destinationType) : null;
        } 
        return returnValue; 
    } 

}
#endif
