import matplotlib.pyplot as plt
import numpy as np
from matplotlib.patches import FancyArrowPatch

class CollisionCheckDemo:
    def __init__(self):
        # Initialize vectors
        self.nextFramePosDisplacement = np.array([0.5, 0.5])
        self.dstToLine = np.array([0.5, -0.5])

        # Setup the plot
        self.fig, self.ax = plt.subplots()
        self.ax.set_xlim(-1, 1)
        self.ax.set_ylim(-1, 1)
        self.ax.set_aspect('equal', adjustable='box')
        self.ax.grid(True)

        # Plot initial vectors
        self.displacement_vector = FancyArrowPatch(posA=(0, 0), posB=self.nextFramePosDisplacement, 
                                                   arrowstyle='->', color='blue', label='Displacement')
        self.dst_vector = FancyArrowPatch(posA=(0, 0), posB=self.dstToLine, 
                                          arrowstyle='->', color='red', label='Dst to Line')
        self.ax.add_patch(self.displacement_vector)
        self.ax.add_patch(self.dst_vector)

        # Text for collision result
        self.collision_text = self.ax.text(0.5, 0.9, "", ha='center', transform=self.ax.transAxes)

        # Update collision check
        self.update_collision_check()

        # Connect event handlers
        self.cid = self.fig.canvas.mpl_connect('button_press_event', self.on_click)

    def update_collision_check(self):
        # Check collision
        collision = self.check_collision(self.nextFramePosDisplacement, self.dstToLine)

        # Update collision text
        self.collision_text.set_text(f"Collision: {'Yes' if collision else 'No'}")
        self.fig.canvas.draw_idle()

    @staticmethod
    def check_collision(nextFramePosDisplacement, dstToLine):
        u_length = np.linalg.norm(dstToLine)
        if u_length == 0:
            return False

        dot_product_vu = np.dot(nextFramePosDisplacement, dstToLine)
        if dot_product_vu < 0:
            return False

        projection_length = abs(dot_product_vu) / u_length
        return projection_length > u_length

    def on_click(self, event):
        if event.inaxes != self.ax:
            return

        # Check if left or right mouse button was pressed
        if event.button == 1:
            # Left click - move nextFramePosDisplacement
            self.nextFramePosDisplacement = np.array([event.xdata, event.ydata])
        elif event.button == 3:
            # Right click - move dstToLine
            self.dstToLine = np.array([event.xdata, event.ydata])

        # Update the plot
        self.displacement_vector.set_positions((0, 0), self.nextFramePosDisplacement)
        self.dst_vector.set_positions((0, 0), self.dstToLine)
        self.update_collision_check()

# Create and show the demo
demo = CollisionCheckDemo()
plt.show()
