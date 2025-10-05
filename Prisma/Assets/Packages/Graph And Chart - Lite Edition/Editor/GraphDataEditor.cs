using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

class GraphDataEditor : UnityEditor.EditorWindow
{
    SerializedObject mThisObject;
    SerializedObject mEditedObject;
    string category;
    SerializedProperty mGraphData;
    SerializedProperty mCategories;
    SerializedProperty mCategory;
    Dictionary<string, SerializedProperty> mValues;
    SerializedObject mObject;
    
    [SerializeField]
    Vector2[] Data;
    public static GraphDataEditor ShowForObject(SerializedObject obj,string category)
    {
        GraphDataEditor window = (GraphDataEditor)EditorWindow.GetWindow(typeof(GraphDataEditor));
        window.SetEditedObject(obj, category);
        return window;
    }

    int FindCategoryIndex(string category)
    {
        for(int i=0; i<mCategories.arraySize; i++)
        {
            string name = mCategories.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue;
            if (name == category)
                return i;
        }
        return -1;
    }
    public void SetEditedObject(SerializedObject obj,string categoryName)
    {
        category = categoryName;
        mEditedObject = obj;
         
        mGraphData = mEditedObject.FindProperty("Data");
        mCategories = mGraphData.FindPropertyRelative("mSerializedData");
        //   LoadData();

        int catIndex = FindCategoryIndex(categoryName);
        if (catIndex == -1)
        {
            mCategory = null;
            return;
        }
        mCategory = mCategories.GetArrayElementAtIndex(catIndex);

        var arr = mCategory.FindPropertyRelative("InitialData");

        mThisObject = new SerializedObject(this);
        SerializedProperty serialProp = mThisObject.FindProperty("Data");
        SetArray(arr, serialProp);
    }

    string getKey(string column, string row)
    {
        return string.Format("{0}|{1}", column, row);
    }

    void ShowCategoryArray()
    {

    }
    void SetArray(SerializedProperty from,SerializedProperty to)
    {
        to.arraySize = from.arraySize;
        for (int i = 0; i < from.arraySize; i++)
        {
            Vector2 val = from.GetArrayElementAtIndex(i).vector2Value;
            to.GetArrayElementAtIndex(i).vector2Value = val;
        }
    }
    void SetArray(Vector2[] from, SerializedProperty to)
    {
        to.arraySize = from.Length;
        for (int i = 0; i < from.Length; i++)
        {
            Vector2 val = from[i];
            to.GetArrayElementAtIndex(i).vector2Value = val;
        }
    }
    Vector2[] FillDataCustomCodeImplementation()
    {
        return new Vector2[] { new Vector2(1, 1), new Vector2(2, 2), new Vector2(3, 3), new Vector2(4, 4) };
    }
    Func<Vector2[]> FillDataCustomCode = null;

    void OnGUI()
    {

        SerializedProperty serialProp = mThisObject.FindProperty("Data");

        GUILayout.Label("Edit Values - " + category, EditorStyles.boldLabel);

        if (mCategory == null)
            return;
        Vector2[] customArr = null;
        //FillDataCustomCode = FillDataCustomCodeImplementation;
        if (FillDataCustomCode != null)
        {
            if (GUILayout.Button("Fill Data From Custom Code"))
            {
                customArr = FillDataCustomCode();
            }
        }
        EditorGUILayout.PropertyField(serialProp, true);
        
        var arr = mCategory.FindPropertyRelative("InitialData");
        if (customArr != null)
        {
            SetArray(customArr, arr);
        }
        else
        {
            if (mThisObject.ApplyModifiedProperties())
                SetArray(serialProp, arr);
        }
        mEditedObject.ApplyModifiedProperties();
    }
}
