import matplotlib.pyplot as plt
import numpy as np
from matplotlib.patches import FancyArrowPatch

class LineSegmentDistanceDemo:
    def __init__(self):
        # Initialize points A, B, and P
        self.A = np.array([0.3, 0.3])
        self.B = np.array([0.7, 0.7])
        self.P = np.array([0.5, 0.5])

        # Setup the plot
        self.fig, self.ax = plt.subplots()
        self.ax.set_xlim(0, 1)
        self.ax.set_ylim(0, 1)
        self.ax.set_aspect('equal', adjustable='box')

        # Plot initial points and line
        self.line, = self.ax.plot([self.A[0], self.B[0]], [self.A[1], self.B[1]], 'bo-')
        self.point, = self.ax.plot(self.P[0], self.P[1], 'ro')

        # Draw initial distance vector
        self.distance_vector = FancyArrowPatch(posA=self.P, posB=self.P, arrowstyle='->', color='green')
        self.ax.add_patch(self.distance_vector)

        # Update the distance vector
        self.update_distance_vector()

        # Connect event handlers
        self.cid = self.fig.canvas.mpl_connect('button_press_event', self.on_click)

    def update_distance_vector(self):
        # Calculate distance vector
        distance_vec = self.signed_distance_to_line_segment(self.A, self.B, self.P)

        # Update arrow
        self.distance_vector.set_positions(self.P, self.P + distance_vec)
        self.fig.canvas.draw_idle()

    @staticmethod
    def signed_distance_to_line_segment(A, B, P):
        AB = B - A
        AP = P - A
        AB_length_squared = np.dot(AB, AB)
        AP_dot_AB = np.dot(AP, AB)
        t = AP_dot_AB / AB_length_squared
        t = np.clip(t, 0.0, 1.0)
        closest_point = A + t * AB
        print(-(P - closest_point))
        return -(P - closest_point)

    def on_click(self, event):
        if event.inaxes != self.ax:
            return

        # Check if left, middle or right mouse button was pressed
        if event.button == 1:
            # Left click - move point A
            self.A = np.array([event.xdata, event.ydata])
        elif event.button == 2:
            # Middle click - move point P
            self.P = np.array([event.xdata, event.ydata])
        elif event.button == 3:
            # Right click - move point B
            self.B = np.array([event.xdata, event.ydata])

        # Update the plot
        self.line.set_data([self.A[0], self.B[0]], [self.A[1], self.B[1]])
        self.point.set_data(self.P[0], self.P[1])
        self.update_distance_vector()

# Create and show the demo
demo = LineSegmentDistanceDemo()
plt.show()
