import XCTest

@MainActor
final class CPUAlertUITests: XCTestCase {
    override func setUpWithError() throws {
        continueAfterFailure = false
    }

    func testMonitorPanelLaunchesAndSwitchesResources() throws {
        let app = XCUIApplication()
        app.launchArguments = ["--open-panel"]
        app.launch()

        let window = app.windows["CPUAlert Monitor"]
        XCTAssertTrue(window.waitForExistence(timeout: 5))
        XCTAssertTrue(window.radioButtons["CPU"].isSelected)

        window.radioButtons["GPU"].click()
        XCTAssertTrue(window.radioButtons["GPU"].isSelected)
        XCTAssertTrue(window.staticTexts["Top process groups"].exists)
    }
}
