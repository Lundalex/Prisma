import matplotlib.pyplot as plt
import numpy as np

def check_line_segments_intersect(A, B, C, D):
    """
    Check if line segments AB and CD intersect.
    """
    def ccw(A, B, C):
        return (C[1] - A[1]) * (B[0] - A[0]) > (B[1] - A[1]) * (C[0] - A[0])

    return ccw(A, C, D) != ccw(B, C, D) and ccw(A, B, C) != ccw(A, B, D)

class LineSegmentIntersectDemo:
    def __init__(self):
        # Initialize line segment endpoints
        self.A = np.array([0.3, 0.3])
        self.B = np.array([0.7, 0.7])
        self.C = np.array([0.2, 0.6])
        self.D = np.array([0.8, 0.4])

        # Setup the plot
        self.fig, self.ax = plt.subplots()
        self.ax.set_xlim(0, 1)
        self.ax.set_ylim(0, 1)
        self.ax.grid(True)

        # Plot initial line segments
        self.line1, = self.ax.plot([self.A[0], self.B[0]], [self.A[1], self.B[1]], 'ro-')
        self.line2, = self.ax.plot([self.C[0], self.D[0]], [self.C[1], self.D[1]], 'bo-')

        # Text for intersection result
        self.intersection_text = self.ax.text(0.5, 0.1, "", ha='center', transform=self.ax.transAxes)
        self.update_intersection_check()

        # Connect event handlers
        self.cid = self.fig.canvas.mpl_connect('button_press_event', self.on_click)

    def update_intersection_check(self):
        # Check intersection
        intersect = check_line_segments_intersect(self.A, self.B, self.C, self.D)

        # Update intersection text
        self.intersection_text.set_text("Intersect: " + ("Yes" if intersect else "No"))
        self.fig.canvas.draw_idle()

    def on_click(self, event):
        if event.inaxes != self.ax:
            return

        # Check which mouse button was pressed
        if event.button == 1:  # Left click - move A or B
            if np.linalg.norm(self.A - [event.xdata, event.ydata]) < np.linalg.norm(self.B - [event.xdata, event.ydata]):
                self.A = np.array([event.xdata, event.ydata])
            else:
                self.B = np.array([event.xdata, event.ydata])
        elif event.button == 3:  # Right click - move C or D
            if np.linalg.norm(self.C - [event.xdata, event.ydata]) < np.linalg.norm(self.D - [event.xdata, event.ydata]):
                self.C = np.array([event.xdata, event.ydata])
            else:
                self.D = np.array([event.xdata, event.ydata])

        # Update the plot
        self.line1.set_data([self.A[0], self.B[0]], [self.A[1], self.B[1]])
        self.line2.set_data([self.C[0], self.D[0]], [self.C[1], self.D[1]])
        self.update_intersection_check()

# Create and show the demo
demo = LineSegmentIntersectDemo()
plt.show()
