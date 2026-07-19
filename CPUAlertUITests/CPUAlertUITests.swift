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
        let disclosure = window.buttons["gpu-group-disclosure-1"]
        XCTAssertTrue(disclosure.waitForExistence(timeout: 2))
        disclosure.click()
        XCTAssertTrue(
            window.descendants(matching: .any)["gpu-group-member-4201"]
                .waitForExistence(timeout: 2)
        )
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
}
