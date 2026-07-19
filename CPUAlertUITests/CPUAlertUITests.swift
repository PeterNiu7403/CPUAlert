import XCTest

@MainActor
final class CPUAlertUITests: XCTestCase {
    override func setUpWithError() throws {
        continueAfterFailure = false
    }

    func testMonitorPanelLaunchesAndSwitchesResources() throws {
        let app = XCUIApplication()
        app.launchArguments = [
            "--ui-testing",
            "--state-green",
            "--rows-5",
            "--expanded-threads",
        ]
        app.launch()

        let window = app.windows["CPUAlert Monitor"]
        XCTAssertTrue(window.waitForExistence(timeout: 5))
        XCTAssertTrue(window.radioButtons["CPU"].isSelected)
        XCTAssertTrue(window.staticTexts["CPUStress"].exists)
        XCTAssertTrue(window.staticTexts["render-loop"].exists)
        XCTAssertFalse(window.staticTexts["Fixture 6"].exists)

        window.radioButtons["GPU"].click()
        XCTAssertTrue(window.radioButtons["GPU"].isSelected)
        XCTAssertTrue(window.staticTexts["Top process groups"].exists)
        XCTAssertTrue(window.staticTexts["Metal Fixture"].exists)
    }

    func testUnavailableGPUAndTenRowsAreDeterministic() throws {
        let app = XCUIApplication()
        app.launchArguments = [
            "--ui-testing",
            "--gpu-unavailable",
            "--rows-10",
        ]
        app.launch()

        let window = app.windows["CPUAlert Monitor"]
        XCTAssertTrue(window.waitForExistence(timeout: 5))
        XCTAssertTrue(window.staticTexts["Fixture 10"].exists)
        XCTAssertTrue(window.staticTexts["Unavailable"].exists)

        window.typeKey(.tab, modifierFlags: [])
        window.buttons["Settings"].click()
        XCTAssertTrue(app.windows.element(boundBy: 1).waitForExistence(timeout: 3))
    }
}
