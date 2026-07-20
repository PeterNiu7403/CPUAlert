import XCTest

@MainActor
final class CPUAlertUITests: XCTestCase {
    override func setUpWithError() throws {
        continueAfterFailure = false
    }

    func testMonitorPanelLaunchesAndSwitchesResources() throws {
        let app = XCUIApplication()
        app.launchArguments = [
            "-AppleLanguages", "(en)",
            "-AppleLocale", "en_US",
            "--ui-testing",
            "--state-green",
            "--rows-5",
            "--expanded-threads",
        ]
        app.launch()

        let window = app.windows["CPUAlert Monitor"]
        XCTAssertTrue(window.waitForExistence(timeout: 5))
        XCTAssertTrue(window.radioButtons["CPU"].exists)
        XCTAssertTrue(window.buttons["cpu-process-disclosure-4201"].exists)
        XCTAssertTrue(window.descendants(matching: .any)["cpu-thread-101"].exists)
        XCTAssertFalse(window.buttons["cpu-process-disclosure-4206"].exists)

        window.radioButtons["GPU"].click()
        XCTAssertTrue(window.staticTexts["Top process groups"].exists)
        let groupCard = window.buttons["gpu-group-card-1"]
        XCTAssertTrue(groupCard.waitForExistence(timeout: 2))
        groupCard.coordinate(withNormalizedOffset: CGVector(dx: 0.18, dy: 0.5)).click()
        XCTAssertTrue(
            window.descendants(matching: .any)["gpu-group-member-4201"]
                .waitForExistence(timeout: 2)
        )

        window.radioButtons["Memory"].click()
        XCTAssertTrue(window.staticTexts["Largest memory footprints"].exists)
        XCTAssertTrue(
            window.descendants(matching: .any)["memory-process-4201"]
                .waitForExistence(timeout: 2)
        )
        window.buttons["memory-cleanup-open"].click()
        XCTAssertTrue(
            window.descendants(matching: .any)["memory-cleanup-candidate-4201"]
                .waitForExistence(timeout: 2)
        )
        let continueButton = window.buttons["memory-cleanup-continue"]
        XCTAssertTrue(continueButton.exists)
        XCTAssertFalse(continueButton.isEnabled)
        XCTAssertTrue(window.buttons["Quit"].exists)
    }

    func testUnavailableGPUAndTenRowsAreDeterministic() throws {
        let app = XCUIApplication()
        app.launchArguments = [
            "-AppleLanguages", "(en)",
            "-AppleLocale", "en_US",
            "--ui-testing",
            "--gpu-unavailable",
            "--rows-10",
        ]
        app.launch()

        let window = app.windows["CPUAlert Monitor"]
        XCTAssertTrue(window.waitForExistence(timeout: 5))
        XCTAssertTrue(window.buttons["cpu-process-disclosure-4210"].exists)
        window.radioButtons["GPU"].click()
        XCTAssertTrue(window.staticTexts["GPU process attribution unavailable"].exists)

        window.buttons["Settings"].click()
        let settingsWindow = app.windows["CPUAlert Settings"]
        XCTAssertTrue(settingsWindow.waitForExistence(timeout: 3))
        XCTAssertTrue(
            settingsWindow.descendants(matching: .any)["settings-launch-at-login"].exists
        )
        XCTAssertTrue(
            settingsWindow.descendants(matching: .any)["settings-visible-rows"].exists
        )
        XCTAssertTrue(settingsWindow.buttons["settings-reset-first-run"].exists)
    }

    func testRealPopoverAllowsContentClicks() throws {
        let app = XCUIApplication()
        app.launchArguments = [
            "-AppleLanguages", "(en)",
            "-AppleLocale", "en_US",
            "--ui-testing",
            "--ui-testing-popover",
            "--state-green",
            "--rows-5",
        ]
        app.launch()

        XCTAssertTrue(app.radioButtons["CPU"].waitForExistence(timeout: 5))

        postUnfocusedClick(at: app.radioButtons["GPU"].frame.center)

        XCTAssertTrue(app.staticTexts["Top process groups"].waitForExistence(timeout: 2))
        let groupCard = app.buttons["gpu-group-card-1"]
        XCTAssertTrue(groupCard.exists)
        postUnfocusedClick(at: groupCard.frame.center)
        XCTAssertTrue(
            app.descendants(matching: .any)["gpu-group-member-4201"]
                .waitForExistence(timeout: 2)
        )

        postUnfocusedClick(at: app.radioButtons["Memory"].frame.center)
        let cleanupButton = app.buttons["memory-cleanup-open"]
        XCTAssertTrue(cleanupButton.waitForExistence(timeout: 2))
        postUnfocusedClick(at: cleanupButton.frame.center)
        let candidate = app.descendants(matching: .any)["memory-cleanup-candidate-4201"]
        XCTAssertTrue(candidate.waitForExistence(timeout: 2))

        postUnfocusedClick(at: candidate.frame.center)

        XCTAssertTrue(candidate.exists)
        XCTAssertTrue(app.buttons["memory-cleanup-continue"].isEnabled)

        let cancelButton = app.buttons["Cancel"]
        XCTAssertTrue(cancelButton.exists)
        postUnfocusedClick(at: cancelButton.frame.center)
        XCTAssertTrue(waitForDisappearance(candidate, timeout: 2))

        postUnfocusedClick(at: CGPoint(x: 100, y: 100))
        XCTAssertTrue(waitForDisappearance(app.radioButtons["CPU"], timeout: 2))
    }

    func testLivePopoverRespondsWhileSampling() throws {
        let app = XCUIApplication()
        app.launchArguments = [
            "-AppleLanguages", "(en)",
            "-AppleLocale", "en_US",
            "--ui-testing",
            "--ui-testing-popover",
            "--live-sampling",
            "--rows-5",
        ]
        app.launch()

        XCTAssertTrue(app.radioButtons["CPU"].waitForExistence(timeout: 5))
        let gpu = app.radioButtons["GPU"]
        XCTAssertTrue(gpu.isHittable)

        let started = ContinuousClock.now
        postUnfocusedClick(at: gpu.frame.center)
        XCTAssertTrue(app.staticTexts["Top process groups"].waitForExistence(timeout: 2))
        XCTAssertLessThan(started.duration(to: .now), .seconds(2))
    }

    private func postUnfocusedClick(at point: CGPoint) {
        let source = CGEventSource(stateID: .hidSystemState)
        CGEvent(
            mouseEventSource: source,
            mouseType: .leftMouseDown,
            mouseCursorPosition: point,
            mouseButton: .left
        )?.post(tap: .cghidEventTap)
        usleep(70_000)
        CGEvent(
            mouseEventSource: source,
            mouseType: .leftMouseUp,
            mouseCursorPosition: point,
            mouseButton: .left
        )?.post(tap: .cghidEventTap)
    }

    private func waitForDisappearance(
        _ element: XCUIElement,
        timeout: TimeInterval
    ) -> Bool {
        let expectation = XCTNSPredicateExpectation(
            predicate: NSPredicate(format: "exists == false"),
            object: element
        )
        return XCTWaiter.wait(for: [expectation], timeout: timeout) == .completed
    }
}

private extension CGRect {
    var center: CGPoint {
        CGPoint(x: midX, y: midY)
    }
}
