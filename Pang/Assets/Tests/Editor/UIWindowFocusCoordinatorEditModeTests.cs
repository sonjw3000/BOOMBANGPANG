using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UniverseLogistics.UI.Toolkit;

public sealed class UIWindowFocusCoordinatorEditModeTests
{
	private const string WindowAssetPath = "Assets/UI/Toolkit/UIWindow/UIWindow.uxml";
	private const string PanelSettingsAssetPath = "Assets/Scripts/UI/Toolkit/New Panel Settings.asset";
	private readonly List<GameObject> windowObjects = new();
	private UIWindowFocusCoordinator coordinator;

	[TearDown]
	public void TearDown()
	{
		coordinator?.Dispose();
		coordinator = null;

		for (int i = windowObjects.Count - 1; i >= 0; --i)
		{
			if (windowObjects[i] != null)
				Object.DestroyImmediate(windowObjects[i]);
		}

		windowObjects.Clear();
	}

	[Test]
	public void BringToFront_ReordersFocusedDocumentsWithinReservedRange()
	{
		coordinator = new UIWindowFocusCoordinator(111f, 129f);
		(UIWindow firstWindow, UIDocument firstDocument) = CreateWindow("First Window");
		(UIWindow secondWindow, UIDocument secondDocument) = CreateWindow("Second Window");
		coordinator.Register(firstWindow, firstDocument);
		coordinator.Register(secondWindow, secondDocument);

		coordinator.BringToFront(firstWindow);
		coordinator.BringToFront(secondWindow);

		Assert.That(firstDocument.sortingOrder, Is.EqualTo(111f));
		Assert.That(secondDocument.sortingOrder, Is.EqualTo(129f));

		coordinator.BringToFront(firstWindow);

		Assert.That(secondDocument.sortingOrder, Is.EqualTo(111f));
		Assert.That(firstDocument.sortingOrder, Is.EqualTo(129f));
	}

	[Test]
	public void BringToFront_DistributesAllFocusedDocumentsWithoutCrossingNoticeLayer()
	{
		coordinator = new UIWindowFocusCoordinator(111f, 129f);
		(UIWindow firstWindow, UIDocument firstDocument) = CreateWindow("First Window");
		(UIWindow secondWindow, UIDocument secondDocument) = CreateWindow("Second Window");
		(UIWindow thirdWindow, UIDocument thirdDocument) = CreateWindow("Third Window");
		coordinator.Register(firstWindow, firstDocument);
		coordinator.Register(secondWindow, secondDocument);
		coordinator.Register(thirdWindow, thirdDocument);

		coordinator.BringToFront(firstWindow);
		coordinator.BringToFront(secondWindow);
		coordinator.BringToFront(thirdWindow);

		Assert.That(firstDocument.sortingOrder, Is.EqualTo(111f));
		Assert.That(secondDocument.sortingOrder, Is.EqualTo(120f));
		Assert.That(thirdDocument.sortingOrder, Is.EqualTo(129f));
		Assert.That(thirdDocument.sortingOrder, Is.LessThan(130f));
	}

	[Test]
	public void Unregister_RemovesDocumentFromFocusedOrdering()
	{
		coordinator = new UIWindowFocusCoordinator(111f, 129f);
		(UIWindow firstWindow, UIDocument firstDocument) = CreateWindow("First Window");
		(UIWindow secondWindow, UIDocument secondDocument) = CreateWindow("Second Window");
		coordinator.Register(firstWindow, firstDocument);
		coordinator.Register(secondWindow, secondDocument);
		coordinator.BringToFront(firstWindow);
		coordinator.BringToFront(secondWindow);

		coordinator.Unregister(secondWindow);

		Assert.That(firstDocument.sortingOrder, Is.EqualTo(111f));
	}

	[Test]
	public void Open_WhenAlreadyOpen_RequestsFocusAgainWithoutRepeatingOpened()
	{
		coordinator = new UIWindowFocusCoordinator(111f, 129f);
		(UIWindow window, UIDocument document) = CreateInitializedWindow("Focusable Window");
		int openedCount = 0;
		int focusRequestedCount = 0;
		window.Opened += () => ++openedCount;
		window.FocusRequested += () => ++focusRequestedCount;

		window.Open();
		window.Open();

		Assert.That(window.IsOpen, Is.True);
		Assert.That(openedCount, Is.EqualTo(1));
		Assert.That(focusRequestedCount, Is.EqualTo(2));
		Assert.That(document.sortingOrder, Is.EqualTo(111f));
	}

	[Test]
	public void Close_TopWindow_ReordersRemainingFocusedWindow()
	{
		coordinator = new UIWindowFocusCoordinator(111f, 129f);
		(UIWindow firstWindow, UIDocument firstDocument) = CreateInitializedWindow("First Focusable Window");
		(UIWindow secondWindow, UIDocument secondDocument) = CreateInitializedWindow("Second Focusable Window");
		firstWindow.Open();
		secondWindow.Open();
		Assert.That(secondDocument.sortingOrder, Is.EqualTo(129f));

		secondWindow.Close();

		Assert.That(secondWindow.IsOpen, Is.False);
		Assert.That(firstDocument.sortingOrder, Is.EqualTo(111f));
	}

	private (UIWindow Window, UIDocument Document) CreateWindow(string name)
	{
		GameObject windowObject = new(name);
		windowObject.SetActive(false);
		UIDocument document = windowObject.AddComponent<UIDocument>();
		UIWindow window = windowObject.AddComponent<UIWindow>();
		windowObjects.Add(windowObject);
		return (window, document);
	}

	private (UIWindow Window, UIDocument Document) CreateInitializedWindow(string name)
	{
		VisualTreeAsset windowAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(WindowAssetPath);
		PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsAssetPath);
		Assert.That(windowAsset, Is.Not.Null);
		Assert.That(panelSettings, Is.Not.Null);

		GameObject windowObject = new(name);
		windowObject.SetActive(false);
		UIDocument document = windowObject.AddComponent<UIDocument>();
		document.panelSettings = panelSettings;
		document.visualTreeAsset = windowAsset;
		UIWindow window = windowObject.AddComponent<UIWindow>();
		window.SetOpenOnEnable(false);
		windowObjects.Add(windowObject);
		coordinator.Register(window, document);
		windowObject.SetActive(true);
		Assert.That(window.Initialize(), Is.True);
		window.Close();
		return (window, document);
	}
}
